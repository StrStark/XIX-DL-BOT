# XIX_DL_BOT

.NET 10 Telegram bot for distributing content gated behind required-channel membership.

## One-shot install (Docker, interactive)

```bash
git clone https://github.com/REPLACE_ME/XIX-DL-BOT.git
cd XIX-DL-BOT
./install.sh
```

The installer asks for your bot token, username, superadmin id, and storage channel id, writes `.env`, builds the Docker image, and starts the container. Run `./install.sh --help` for flags.

## Quick start (local dev, polling mode)

```bash
cp .env.example .env
# Edit .env: fill in BOT_STORAGE_CHANNEL_ID (negative chat id, bot must be admin)
dotnet run --project src/DownloaderBot
```

Required env vars: `BOT_TOKEN`, `BOT_USERNAME`, `BOT_SUPERADMIN_TG_ID`, `BOT_STORAGE_CHANNEL_ID`.

Switch between polling and webhook with `BOT_UPDATE_MODE=polling|webhook`. Webhook mode also requires `BOT_WEBHOOK_URL`, `BOT_WEBHOOK_SECRET`, `BOT_WEBHOOK_LISTEN`.

## Docker

```bash
docker compose up -d --build
```

Volumes are created for db, backups, logs. Edit `welcome.txt` and `.env` on the host before bringing up the container — they are bind-mounted.

## Admin

The user id in `BOT_SUPERADMIN_TG_ID` is inserted as the first superadmin on startup. Open the bot in Telegram and send `/admin`.

## Users

Users can only run `/start` (with or without a payload). All other messages are silently ignored.

## License

Private.
