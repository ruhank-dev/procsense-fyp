"""Headers on every response (p1-backend-s11). A page may only run scripts that come from this service, load nothing from anywhere else, and cannot be put in a frame by another site.

A route that needs different rules sets its own header first (the session report page does) and is left alone.
"""
from __future__ import annotations

from urllib.parse import urlsplit

from starlette.datastructures import MutableHeaders
from starlette.types import ASGIApp, Message, Receive, Scope, Send

from server.config import get_settings


def page_policy(supabase_url: str) -> str:
    """The content-security policy of the web pages. Scripts and styles come from this service only (no inline script, no eval); the pages may talk to this service and to the account system.
    Inline styles are allowed because the session report is shown in a sandboxed frame made from the report's own page, which carries its style inline; it still cannot run script."""
    origin = ""
    if supabase_url:
        parts = urlsplit(supabase_url)
        origin = f"{parts.scheme}://{parts.netloc}" if parts.scheme and parts.netloc else ""
    connect = "'self'" + (" " + origin if origin else "")
    return ("default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src " + connect +
            "; frame-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'; object-src 'none'")


API_POLICY = "default-src 'none'; frame-ancestors 'none'"


class SecurityHeaders:
    def __init__(self, app: ASGIApp):
        self.app = app

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return

        async def send_with_headers(message: Message) -> None:
            if message["type"] == "http.response.start":
                settings = get_settings()
                headers = MutableHeaders(scope=message)
                is_page = "text/html" in headers.get("content-type", "")
                defaults = {
                    "Content-Security-Policy": page_policy(settings.supabase_url) if is_page else API_POLICY,
                    "X-Content-Type-Options": "nosniff",
                    "X-Frame-Options": "DENY",
                    "Referrer-Policy": "no-referrer",
                    "Permissions-Policy": "camera=(), microphone=(), geolocation=(), payment=()",
                    "Cross-Origin-Opener-Policy": "same-origin",
                }
                if settings.app_env == "prod":
                    defaults["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains"
                for name, value in defaults.items():
                    if name not in headers:
                        headers[name] = value
            await send(message)

        await self.app(scope, receive, send_with_headers)
