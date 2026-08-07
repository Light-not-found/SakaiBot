# SakaiBot

This repository contains a .NET 10 Discord bot packaged as a Docker container.

## What’s included

- `Dockerfile` — builds the app and publishes a container image.
- `.github/workflows/docker-publish.yml` — builds and pushes the Docker image to GitHub Container Registry on `main`.
- `.github/workflows/render-deploy.yml` — triggers a Render deploy after the Docker build workflow succeeds.
- `src/SakaiBot` — the bot application.

## Local build and test

From the repository root:

```powershell
cd /d C:\Users\devan\SakaiBot
docker build -t sakaibot:test .
```

Run the container locally:

```powershell
docker run -p 8080:8080 -e DISCORD__TOKEN="your_token_here" sakaibot:test
```

Then verify the health endpoint:

```powershell
curl http://localhost:8080/health
```

## GitHub Actions

### Build and publish Docker image

On push to `main`, `docker-publish.yml` will:

- checkout the repository
- build the Docker image from `Dockerfile`
- push the image to GitHub Container Registry as `ghcr.io/<owner>/sakaibot:latest`

### Trigger Render deploy

`render-deploy.yml` runs after the Docker publish workflow completes successfully and calls the Render API to start a new deploy.

## Render setup

### Required Render secrets

Set these repository secrets in GitHub under `Settings -> Secrets and variables -> Actions`:

- `RENDER_API_KEY`
- `RENDER_SERVICE_ID`

### Render service configuration

Create a Render Web Service and configure it to use your repo or the Docker image.

If using the repo Dockerfile:
- Service type: Web Service
- Environment: Docker
- Port: `8080`
- Health check path: `/health`
- Add environment variable `DISCORD__TOKEN`

If using `ghcr.io` private image:
- Configure Render to pull from GitHub Container Registry
- Set image to `ghcr.io/<owner>/sakaibot:latest`
- Set environment variable `DISCORD__TOKEN`

## Environment variables

Do not commit `.env` or secrets into GitHub. Use repository secrets or Render environment variables instead.

Required runtime variables:

- `DISCORD__TOKEN`
- `DOTNET_ENVIRONMENT=production`
- any database or external config values your bot needs

## Security note

If any secret was ever committed, rotate it immediately.

- regenerate your Discord bot token
- rotate database credentials
- remove secrets from Git history if needed

## Useful commands

```powershell
# build locally from repository root
cd /d C:\Users\devan\SakaiBot
docker build -t sakaibot:test .

# run locally
docker run -p 8080:8080 -e DISCORD__TOKEN="your_token_here" sakaibot:test
```
