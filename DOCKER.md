# Docker Setup Guide

## Prerequisites
- Docker Desktop installed and running
- Docker Compose available

## Quick Start

### 1. Set up environment variables
```bash
cp .env.template .env
# Edit .env and set a secure DB_PASSWORD
```

### 2. Start PostgreSQL
```bash
docker-compose up -d
```

### 3. Verify it's running
```bash
docker-compose ps
```

You should see:
```
NAME                  COMMAND                  SERVICE    STATUS
spotify-tools-db      "docker-entrypoint.s…"   postgres   Up (healthy)
```

### 4. Connect to the database

**Connection String:**
```
Host=localhost;Port=5432;Database=spotify_tools;Username=spotify_user;Password=<your_password>
```

**Using psql:**
```bash
docker exec -it spotify-tools-db psql -U spotify_user -d spotify_tools
```

## Docker Commands

### Stop the database
```bash
docker-compose stop
```

### Start the database
```bash
docker-compose start
```

### Stop and remove containers (data persists in volume)
```bash
docker-compose down
```

### Stop and remove everything including data
```bash
docker-compose down -v
```

### View logs
```bash
docker-compose logs -f postgres
```

### Backup database
```bash
docker exec -t spotify-tools-db pg_dump -U spotify_user spotify_tools > backup_$(date +%Y%m%d_%H%M%S).sql
```

### Restore database
```bash
cat backup_file.sql | docker exec -i spotify-tools-db psql -U spotify_user -d spotify_tools
```

## Database Configuration

- **Image:** PostgreSQL 16 Alpine (lightweight)
- **Port:** 5432 (mapped to host)
- **Database:** spotify_tools
- **User:** spotify_user
- **Password:** Set via .env file (DB_PASSWORD)
- **Data Volume:** postgres_data (persists across restarts)

## Healthcheck

The container includes a healthcheck that verifies PostgreSQL is ready to accept connections. The status shows as "healthy" when ready.

## Troubleshooting

### Port 5432 already in use
If you have PostgreSQL installed locally, either:
1. Stop the local PostgreSQL service
2. Change the port mapping in docker-compose.yml (e.g., "5433:5432")

### Container won't start
Check logs:
```bash
docker-compose logs postgres
```

### Can't connect from application
1. Ensure container is healthy: `docker-compose ps`
2. Verify connection string matches docker-compose.yml settings
3. Check firewall/network settings
