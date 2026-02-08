# Email System

The backend uses a **single email abstraction** so you can send mail from anywhere and swap providers (SMTP, SendGrid, SES) without changing callers. For **multiple, heavily styled emails**, use **templates** so HTML lives in files, not in code.

---

## Template-based sending (recommended for multiple / styled emails)

Use **`IEmailTemplateService`** when you have many email types or large/styled HTML. Templates are **files** in `EmailTemplates/` (or the path in `EmailSettings:TemplateBasePath`).

### How it works

1. Add two files per email type: **`{Name}.html`** and optionally **`{Name}.txt`** (for plain text).
2. In the HTML/text, use placeholders: **`{{Code}}`**, **`{{UserName}}`**, **`{{ExpiresInMinutes}}`**, etc.
3. In code, call `SendTemplateAsync` with the template name and a dictionary of values.

**Example: handler code stays small**

```csharp
var model = new Dictionary<string, string>
{
    ["Code"] = code,
    ["ExpiresInMinutes"] = "10"
};
await _emailTemplateService.SendTemplateAsync(
    "RegistrationOtp",
    model,
    to: email,
    subject: "Verify your email for registration",
    cancellationToken: cancellationToken);
```

All styling and long HTML live in **`EmailTemplates/RegistrationOtp.html`** (and optionally `RegistrationOtp.txt`). You can add as many templates as you need: `OrderShipped.html`, `OrderConfirmed.html`, `Welcome.html`, etc.

### Placeholders

- Use **`{{PlaceholderName}}`** in the template (alphanumeric names).
- Pass the same keys in the `model` dictionary when calling `SendTemplateAsync`.
- Missing placeholders are left as-is (e.g. `{{Unknown}}`).

### Where templates live

- Default folder: **`EmailTemplates`** under the API project (next to `Program.cs`).
- Override with **`EmailSettings:TemplateBasePath`** in appsettings (e.g. another folder or absolute path).
- Templates are copied to output and publish so they work when you run or deploy.

---

## Direct email (simple one-off messages)

For very simple or one-off emails, inject **`IEmailService`** and call **`SendAsync`** with an **`EmailMessage`**:

```csharp
var message = new EmailMessage
{
    To = to,
    ToName = userName,
    Subject = "Your order shipped",
    BodyPlainText = "Your order has shipped.",
    BodyHtml = "<p>Your order has <strong>shipped</strong>.</p>"
};
await _emailService.SendAsync(message, ct);
```

- **To** / **ToName**: Recipient.
- **Subject**: Email subject.
- **BodyPlainText**: Plain-text body (always set for fallback).
- **BodyHtml**: Optional HTML body.
- **TemplateId** / **TemplateData**: For providers that support templates (e.g. SendGrid); current SMTP implementation ignores these.

---

## Configuration

Settings under **`EmailSettings`** in `appsettings.json` (or env vars, see `env.example`):

| Setting           | Description                                                       |
|-------------------|-------------------------------------------------------------------|
| SmtpHost          | SMTP server (e.g. `smtp.gmail.com`)                               |
| SmtpPort          | Usually 587 (TLS) or 465 (SSL)                                    |
| UseSsl            | Use StartTLS (true for port 587)                                  |
| SmtpUser          | SMTP username                                                     |
| SmtpPassword      | SMTP password (e.g. Gmail App Password)                           |
| FromAddress       | Sender email                                                      |
| FromName          | Sender display name                                              |
| DisableSending    | If true, no email is sent (e.g. for local dev)                     |
| TemplateBasePath  | Folder for template files (default: `EmailTemplates`)              |

---

## Auth flows using email

- **Forgot password**: `POST /api/auth/forgot-password` with `{ "email": "..." }` → sends OTP; then `POST /api/auth/reset-password` with `{ "email", "otp", "newPassword", "confirmPassword" }`.
- **Register with OTP**: `POST /api/auth/send-registration-otp` with `{ "email": "..." }` → sends OTP; then `POST /api/auth/register` with same email and `"otp": "123456"` (plus password, firstName, lastName). If `otp` is provided and valid, the user is created with `EmailVerified = true`.

---

## Adding another provider (e.g. SendGrid)

1. Implement **`IEmailService`** in a new class (e.g. `SendGridEmailService`).
2. In **InfrastructureServiceRegistration**, register that implementation when you want to use it (e.g. via config or environment).
3. All callers keep using **`IEmailService.SendAsync(EmailMessage)`** (and **`IEmailTemplateService`** still builds `EmailMessage` and calls this); no other code changes needed.

---

## OTP storage

OTPs are stored in **Redis** (same cache as refresh tokens), key pattern `DecorativePlant:otp:{Purpose}:{email}`, with a short TTL. Purpose values: `Registration`, `PasswordReset`. One-time use is enforced by deleting the key on successful verification. No database table is used for OTPs.
