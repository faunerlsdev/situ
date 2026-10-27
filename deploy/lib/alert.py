#!/usr/bin/env python3
"""Send an operational alert. The message body is not written to the log."""

import os
import smtplib
import sys
from email.message import EmailMessage


def main() -> int:
    if len(sys.argv) != 3:
        print("uso: alert.py <asunto> <archivo-cuerpo>", file=sys.stderr)
        return 2
    subject, body_path = sys.argv[1], sys.argv[2]
    host = os.environ.get("SMTP_HOST", "")
    recipient = os.environ.get("ALERT_TO", "")
    sender = os.environ.get("SMTP_FROM", "")
    if not host or not recipient or not sender:
        print("alerta no enviada: faltan SMTP_HOST, SMTP_FROM o ALERT_TO", file=sys.stderr)
        return 1
    with open(body_path, encoding="utf-8") as handle:
        body = handle.read()
    message = EmailMessage()
    message["Subject"] = subject
    message["From"] = sender
    message["To"] = recipient
    message.set_content(body)
    port = int(os.environ.get("SMTP_PORT", "587"))
    use_ssl = os.environ.get("SMTP_SSL", "true").lower() == "true"
    try:
        if use_ssl and port == 465:
            client = smtplib.SMTP_SSL(host, port, timeout=30)
        else:
            client = smtplib.SMTP(host, port, timeout=30)
            client.ehlo()
            if use_ssl:
                client.starttls()
                client.ehlo()
        username = os.environ.get("SMTP_USER", "")
        password = os.environ.get("SMTP_PASSWORD", "")
        if username:
            client.login(username, password)
        client.send_message(message)
        client.quit()
    except Exception as exc:  # noqa: BLE001 — operational alert, no body in the log
        print(f"alerta no enviada ({type(exc).__name__})", file=sys.stderr)
        return 1
    print("alerta enviada")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
