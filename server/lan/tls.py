"""Self-signed certificate for the optional HTTPS of the LAN demonstration (P3-NET-15).

`task demo-laptop -- --tls` makes (or reuses) a certificate valid for this laptop's private addresses, and prints its SHA-256 fingerprint. The fingerprint is what the headset pins at pairing:
a different certificate (another laptop, or someone in the middle) has another fingerprint and is refused. The private key stays in this folder on the laptop and is never sent anywhere."""
from __future__ import annotations

import datetime as dt
import hashlib
import ipaddress
import os
from pathlib import Path

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.x509.oid import NameOID

VALID_DAYS = 365


def fingerprint(cert_pem: bytes) -> str:
    """SHA-256 of the certificate (DER), lower-case hex: the value a headset pins."""
    der = x509.load_pem_x509_certificate(cert_pem).public_bytes(serialization.Encoding.DER)
    return hashlib.sha256(der).hexdigest()


def _addresses(cert: x509.Certificate) -> set[str]:
    san = cert.extensions.get_extension_for_class(x509.SubjectAlternativeName).value
    return {str(ip) for ip in san.get_values_for_type(x509.IPAddress)}


def make(ips: list[str], now: dt.datetime | None = None) -> tuple[bytes, bytes]:
    now = now or dt.datetime.now(dt.timezone.utc)
    key = ec.generate_private_key(ec.SECP256R1())
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "ProcSense LAN demonstration")])
    names: list[x509.GeneralName] = [x509.DNSName("localhost"), x509.IPAddress(ipaddress.ip_address("127.0.0.1"))]
    names += [x509.IPAddress(ipaddress.ip_address(ip)) for ip in sorted(set(ips) - {"127.0.0.1"})]
    cert = (x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(key.public_key()).serial_number(x509.random_serial_number())
            .not_valid_before(now - dt.timedelta(minutes=5)).not_valid_after(now + dt.timedelta(days=VALID_DAYS))
            .add_extension(x509.SubjectAlternativeName(names), critical=False)
            .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
            .sign(key, hashes.SHA256()))
    return (cert.public_bytes(serialization.Encoding.PEM),
            key.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8, serialization.NoEncryption()))


def ensure(folder: Path, ips: list[str], now: dt.datetime | None = None) -> tuple[Path, Path, str]:
    """(certificate file, key file, fingerprint). An existing certificate is reused while it still covers every address and has more than a week left, so the headset's pin stays valid across restarts."""
    now = now or dt.datetime.now(dt.timezone.utc)
    cert_file, key_file = folder / "demo-cert.pem", folder / "demo-key.pem"
    if cert_file.exists() and key_file.exists():
        try:
            pem = cert_file.read_bytes()
            cert = x509.load_pem_x509_certificate(pem)
            if set(ips) | {"127.0.0.1"} <= _addresses(cert) and cert.not_valid_after_utc > now + dt.timedelta(days=7):
                return cert_file, key_file, fingerprint(pem)
        except (OSError, ValueError):
            pass
    cert_pem, key_pem = make(ips, now)
    folder.mkdir(parents=True, exist_ok=True)
    cert_file.write_bytes(cert_pem)
    key_file.write_bytes(key_pem)
    try:
        os.chmod(key_file, 0o600)
    except OSError:
        pass
    return cert_file, key_file, fingerprint(cert_pem)
