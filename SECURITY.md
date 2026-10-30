# Security policy

## Report a problem

Please report security problems privately. Do not open a public issue.

Use GitHub: open the **Security** tab of this repository and choose **Report a vulnerability**
(<https://github.com/rzerdik/league-remote/security/advisories/new>).

Include what you found, how to reproduce it, and the version. I will answer as soon as I can, and credit you in the fix
if you want.

## Supported versions

Only the latest release gets fixes.

## In scope

- Pairing, tokens and the brute-force guards.
- The local certificate authority and the certificates it issues.
- The command whitelist: anything that lets a phone send a request the list does not allow.
- Anything that exposes the League client's password, tokens, chat text, or enemy players' names or ids.
- The web server on ports 5080 and 5443.

## Out of scope

- Someone who already has your pairing code, your phone, or your Windows account.
- Other devices on your network eavesdropping on traffic. There is no end-to-end encryption. See
  [docs/security.md](docs/security.md).
- Bugs in the League client or in Riot's services.
- The exe being unsigned.

How the app is protected: [docs/security.md](docs/security.md).
