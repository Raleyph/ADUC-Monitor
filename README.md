# ADUC Monitoring

This is a small script for Microsoft Active Directory that sends an email from your own SMTP server, 
when a new user is added.

## Application settings

There are only SMTP settings:
- **Sender** — the outgoing email address (e.g. notification@mydomain.com).
- **Recipient** — list of recipients.
- **Host** — SMTP server host (e.g. mail.mydomain.com)
- **Port** — SMTP server port (usually 25, 465 or 587)
- **EnableSsl** — SSL setting. Depend on your mail server settings.
- **Login** - SMTP server login.
- **Password** - SMTP server password.