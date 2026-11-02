# Security

League Remote runs a small web server on your PC and lets a paired phone control your League client.
This page says what is exposed and how it is protected. To report a problem, see [../SECURITY.md](../SECURITY.md).

## What is exposed

The app listens on every IPv4 address of the PC (not on IPv6: the certificate authority below only vouches for private
IPv4 addresses and `localhost`):

| Port | Protocol | Serves |
|---|---|---|
| 5080 (`Server:HttpPort`) | HTTP | The setup page and the certificate download. Everything else redirects to HTTPS on the address the phone used (a `Host` header the certificate cannot cover is not reflected). The hub over plain HTTP works from the PC itself only. |
| 5443 (`Server:HttpsPort`) | HTTPS | The web app and the hub (`/hub`). |

Anyone on the same network can reach these ports, so allow them on **Private** networks only. The web app, `/healthz`
and the client sounds (`/client-sounds`) are open. The hub needs a pairing code or a token.

There is no end-to-end encryption: traffic is encrypted between the phone and the PC, and nothing else is involved.
Use a network you trust.

### Browser hardening

Every response carries `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`, a
`Permissions-Policy` that allows only the camera (for the QR scanner) and a `Content-Security-Policy`. The policy lets the
web app load only its own files, Data Dragon images and data (`ddragon.leagueoflegends.com`), its own WebSocket, and the
WebAssembly QR decoder it ships; inline scripts, frames, plugins and foreign form targets are blocked. The exact policy is
`SecurityHeaders.ContentSecurityPolicy` in `src/LeagueRemote.Connector/Server/SecurityHeaders.cs`. Client sounds and rank
emblems come from the PC app, which reads a fixed list of them from the local League install. A tap on a push
notification only ever opens a page of the app itself.

## Local certificate authority

On first start the app creates its own certificate authority (CA) so the phone can use HTTPS on a private IP.

- The CA ("League Remote local CA", RSA 2048, 10 years) signs a server certificate for the PC's private IPv4
  addresses, `localhost` and `127.0.0.1`. The server certificate lasts 397 days and is renewed when it is close to expiry
  or the addresses change.
- Both live in `%LOCALAPPDATA%\league-remote\certs\` as PKCS#12 files encrypted with DPAPI (your Windows user).
  Only the CA's public certificate is ever served (`/league-remote-ca.crt`). The private key never leaves the folder.
- The CA has a critical **name constraint**. It can only vouch for private IPv4 ranges (10/8, 172.16/12, 192.168/16,
  127/8, 169.254/16) and `localhost`. A stolen CA key cannot be used to impersonate a public website.
- The setup page is plain HTTP, because the phone cannot trust HTTPS yet. It shows the CA's SHA-256 fingerprint, which
  you can compare with the one in the phone's certificate details. Someone who controls your network could change both
  the page and the certificate, so set up a phone only on a network you trust.

To remove it, delete the CA from the phone (Android: Trusted credentials, User. iPhone: Settings, General, VPN and
Device Management) and delete the `certs` folder on the PC.

## Pairing and tokens

- The tray shows a 6-digit code, also as a QR link. The code stays valid until the app restarts, you choose
  **Unpair All Phones** in the tray, or the brute-force guard changes it.
- A phone that enters the code gets a random 256-bit token. The PC stores only a SHA-256 hash of it
  (`phones.json`). The phone keeps the token in `localStorage`.
- Up to 10 phones can be paired. Past that, the least recently used one that is not connected is replaced.
- **Unpair This Phone** in the phone's settings removes that phone. **Unpair All Phones** in the tray removes every
  phone, closes their connections and changes the code.

### Brute-force guards

Wrong codes are limited at three levels:

- 5 failures per connection.
- 20 failures per address in 10 minutes.
- A global guard: more than 10 failures in 10 minutes, at most 5 of them from one address, changes the code.
  Paired phones keep working.

A code that was valid recently (replaced or old) is told so, and counts only against its own connection.
Signing in with a token is not limited, because tokens are too long to guess.

Anyone who can see the code or QR can pair a phone. Treat the tray window like a password.

## What the phone can see and do

- **Commands:** only the fixed list in [commands.md](commands.md). The PC checks each one against the list before it
  runs it, and only on what the client lists right now. The app has typed methods for those endpoints only. There is
  no raw proxy.
- **State:** the phone gets your game phase, ready check, own lobby, own team in champ select, your runes and skins,
  your friends and chats. Of the enemy team it gets only what the client shows: champions, positions and bans.
  Never enemy names, puuids or summoner ids.
- **Logs:** the app never logs chat text, tokens or the League password.

## The League client

The client's local API uses a self-signed certificate. The app accepts it only for `127.0.0.1`, and the Live Client
Data API only for `127.0.0.1:2999`. The client's port and password are read again whenever it restarts.
The app only reads from the Live Client Data API. It never sends anything to the game.

## Web Push

Push messages are encrypted to the phone (RFC 8291). The push service sees the endpoint and timing only.
Only paired phones can register, and only with the `https` addresses of the browsers' push services (Google, Mozilla,
Apple, Windows), so the app cannot be used to send requests into your network. A phone can change, remove or test only
the subscription it registered itself. The PC's signing key pair (`vapid.dpapi`) is protected with DPAPI like the
certificates. Messages never contain chat text. Details: [notifications-and-sounds.md](notifications-and-sounds.md).

## Known limits

- Anyone on your network who has the code can pair. Anyone with several addresses who keeps failing can keep changing
  your code. It is a nuisance, not a way in.
- Tokens do not expire. They are removed on unpair.
- The League client's certificate is accepted by host, not checked against Riot's chain.
- The exe is not code-signed.
