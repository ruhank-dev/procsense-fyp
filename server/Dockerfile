# The ProcSense service image. Build from the repository root:  docker build -f server/Dockerfile -t procsense-server .
# Two stages: the first installs the locked dependencies, the second is a small runtime that runs as an ordinary user.

FROM python:3.12-slim AS builder
COPY --from=ghcr.io/astral-sh/uv:0.13 /uv /bin/uv
ENV UV_COMPILE_BYTECODE=1 UV_LINK_MODE=copy UV_PROJECT_ENVIRONMENT=/opt/venv
WORKDIR /app
COPY server/pyproject.toml server/uv.lock server/.python-version ./server/
RUN uv sync --project server --frozen --no-dev

FROM python:3.12-slim AS runtime
ENV PYTHONUNBUFFERED=1 PYTHONDONTWRITEBYTECODE=1 PATH="/opt/venv/bin:$PATH" PORT=8000
RUN useradd --create-home --uid 10001 app
WORKDIR /app
COPY --from=builder /opt/venv /opt/venv
COPY server ./server
# the engine and the content it verifies with, prepared by `task server-engine-prepare` (CI does it before the image is built)
COPY server/engine /opt/engine
ENV ENGINE_CLI=/opt/engine/bin/procsense-sim ENGINE_CONTENT_ROOT=/opt/engine/content DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/dotnet-bundle DOTNET_CLI_TELEMETRY_OPTOUT=1
USER app
EXPOSE 8000
# a plain request to /health (it does not touch the database, so a sleeping database never makes the container look dead)
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s CMD ["python", "-c", "import os,urllib.request; urllib.request.urlopen('http://127.0.0.1:%s/health' % os.environ.get('PORT','8000'), timeout=4)"]
CMD ["sh", "-c", "exec uvicorn server.main:app --host 0.0.0.0 --port ${PORT}"]
