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
Host=localhost;Port=5433;Database=spotify_tools;Username=spotify_user;Password=<your_password>
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
- **Port:** 5433 (mapped to host, internal container port is 5432)
- **Database:** spotify_tools
- **User:** spotify_user
- **Password:** Set via .env file (DB_PASSWORD)
- **Data Volume:** postgres_data (persists across restarts)

## Healthcheck

The container includes a healthcheck that verifies PostgreSQL is ready to accept connections. The status shows as "healthy" when ready.

## Troubleshooting

### Port 5433 already in use
If you need to use a different port, change the port mapping in docker-compose.yml (e.g., "5434:5432") and update your connection strings accordingly.

### Container won't start
Check logs:
```bash
docker-compose logs postgres
```

### Can't connect from application
1. Ensure container is healthy: `docker-compose ps`
2. Verify connection string matches docker-compose.yml settings
3. Check firewall/network settings

## Connecting with DataGrip

DataGrip is a powerful database IDE that provides excellent visualization and query capabilities.

### Connection Settings:
- **Host:** localhost
- **Port:** 5433
- **Database:** spotify_tools
- **User:** spotify_user
- **Password:** (from your .env file)
- **URL:** `jdbc:postgresql://localhost:5433/spotify_tools`

### Testing Connection:
1. Open DataGrip
2. Create new Data Source → PostgreSQL
3. Enter connection details above
4. Click "Test Connection"
5. Click "OK" to save

## Database Views

The database includes 8 pre-built views for analytics and visualization:

### Analytics Views

1. **v_tracks_with_artists** - Denormalized tracks with all artist details
2. **v_tracks_with_albums** - Tracks with album information
3. **v_track_complete_details** - Complete track details (artists, genres, albums, audio features aggregated)
4. **v_playlist_contents** - Playlist contents with track and artist details
5. **v_genre_stats** - Genre statistics with track counts, popularity, and audio feature averages
6. **v_artist_performance** - Artist metrics including track counts and averages
7. **v_sync_summary** - Human-readable sync history with duration calculations
8. **v_high_energy_tracks** - Pre-filtered high-energy tracks (energy > 0.7, danceability > 0.6)

### Example Queries

```sql
-- Top genres by track count
SELECT genre, track_count, artist_count, total_followers
FROM v_genre_stats
LIMIT 20;

-- All tracks by a specific artist
SELECT track_name, album_name, release_date, popularity
FROM v_track_complete_details
WHERE artists LIKE '%Artist Name%';

-- Playlist analysis
SELECT playlist_name, COUNT(*) as track_count, AVG(popularity) as avg_popularity
FROM v_playlist_contents
GROUP BY playlist_id, playlist_name
ORDER BY track_count DESC;
```

## Creating Custom Views and Functions

PostgreSQL supports views and PL/pgSQL functions (similar to SQL Server stored procedures).

### Creating a View:
```sql
CREATE OR REPLACE VIEW my_custom_view AS
SELECT column1, column2
FROM table_name
WHERE condition;
```

### Creating a Function:
```sql
CREATE OR REPLACE FUNCTION get_artist_tracks(artist_name TEXT)
RETURNS TABLE(track_name TEXT, popularity INTEGER) AS $$
BEGIN
    RETURN QUERY
    SELECT t.name, t.popularity
    FROM tracks t
    JOIN track_artists ta ON t.id = ta.track_id
    JOIN artists a ON ta.artist_id = a.id
    WHERE a.name ILIKE '%' || artist_name || '%';
END;
$$ LANGUAGE plpgsql;
```

### Calling a Function:
```sql
SELECT * FROM get_artist_tracks('Beatles');
```
