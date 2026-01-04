-- Analytics Views for Play History Tracking

-- 1. Track play counts with basic stats
CREATE OR REPLACE VIEW v_track_play_counts AS
SELECT
    t.id,
    t.name,
    COUNT(ph.id) as play_count,
    MAX(ph.played_at) as last_played,
    MIN(ph.played_at) as first_played
FROM tracks t
INNER JOIN play_history ph ON t.id = ph.track_id
GROUP BY t.id, t.name;

-- 2. Most played tracks with artist info
CREATE OR REPLACE VIEW v_most_played_tracks AS
SELECT
    t.id,
    t.name as track_name,
    STRING_AGG(DISTINCT a.name, ', ' ORDER BY a.name) as artists,
    COUNT(ph.id) as play_count,
    MAX(ph.played_at) as last_played,
    t.duration_ms,
    t.popularity
FROM tracks t
INNER JOIN play_history ph ON t.id = ph.track_id
INNER JOIN track_artists ta ON t.id = ta.track_id
INNER JOIN artists a ON ta.artist_id = a.id
GROUP BY t.id, t.name, t.duration_ms, t.popularity
ORDER BY play_count DESC;

-- 3. Plays by date (daily aggregation)
CREATE OR REPLACE VIEW v_plays_by_date AS
SELECT
    DATE(ph.played_at) as play_date,
    COUNT(*) as play_count,
    COUNT(DISTINCT ph.track_id) as unique_tracks
FROM play_history ph
GROUP BY DATE(ph.played_at)
ORDER BY play_date DESC;

-- 4. Listening patterns by hour of day
CREATE OR REPLACE VIEW v_plays_by_hour AS
SELECT
    EXTRACT(HOUR FROM ph.played_at) as hour,
    COUNT(*) as play_count,
    COUNT(DISTINCT ph.track_id) as unique_tracks
FROM play_history ph
GROUP BY EXTRACT(HOUR FROM ph.played_at)
ORDER BY hour;

-- 5. Listening patterns by day of week (0=Sunday, 6=Saturday)
CREATE OR REPLACE VIEW v_plays_by_day_of_week AS
SELECT
    EXTRACT(DOW FROM ph.played_at) as day_of_week,
    CASE EXTRACT(DOW FROM ph.played_at)
        WHEN 0 THEN 'Sunday'
        WHEN 1 THEN 'Monday'
        WHEN 2 THEN 'Tuesday'
        WHEN 3 THEN 'Wednesday'
        WHEN 4 THEN 'Thursday'
        WHEN 5 THEN 'Friday'
        WHEN 6 THEN 'Saturday'
    END as day_name,
    COUNT(*) as play_count,
    COUNT(DISTINCT ph.track_id) as unique_tracks
FROM play_history ph
GROUP BY EXTRACT(DOW FROM ph.played_at)
ORDER BY day_of_week;

-- 6. Genre play counts
CREATE OR REPLACE VIEW v_genre_play_counts AS
SELECT
    UNNEST(a.genres) as genre,
    COUNT(*) as play_count,
    COUNT(DISTINCT t.id) as unique_tracks,
    COUNT(DISTINCT a.id) as unique_artists
FROM play_history ph
INNER JOIN tracks t ON ph.track_id = t.id
INNER JOIN track_artists ta ON t.id = ta.track_id
INNER JOIN artists a ON ta.artist_id = a.id
WHERE a.genres IS NOT NULL AND array_length(a.genres, 1) > 0
GROUP BY UNNEST(a.genres)
ORDER BY play_count DESC;

-- 7. Artist play counts
CREATE OR REPLACE VIEW v_artist_play_counts AS
SELECT
    a.id,
    a.name as artist_name,
    COUNT(*) as play_count,
    COUNT(DISTINCT t.id) as unique_tracks,
    MAX(ph.played_at) as last_played
FROM play_history ph
INNER JOIN tracks t ON ph.track_id = t.id
INNER JOIN track_artists ta ON t.id = ta.track_id
INNER JOIN artists a ON ta.artist_id = a.id
GROUP BY a.id, a.name
ORDER BY play_count DESC;

-- 8. Context type distribution (where tracks are played)
CREATE OR REPLACE VIEW v_plays_by_context AS
SELECT
    COALESCE(ph.context_type, 'unknown') as context_type,
    COUNT(*) as play_count,
    COUNT(DISTINCT ph.track_id) as unique_tracks
FROM play_history ph
GROUP BY COALESCE(ph.context_type, 'unknown')
ORDER BY play_count DESC;

-- 9. Recent listening activity (last 100 plays)
CREATE OR REPLACE VIEW v_recent_plays AS
SELECT
    ph.id,
    ph.played_at,
    t.name as track_name,
    STRING_AGG(DISTINCT a.name, ', ' ORDER BY a.name) as artists,
    ph.context_type,
    t.duration_ms
FROM play_history ph
INNER JOIN tracks t ON ph.track_id = t.id
INNER JOIN track_artists ta ON t.id = ta.track_id
INNER JOIN artists a ON ta.artist_id = a.id
GROUP BY ph.id, ph.played_at, t.name, ph.context_type, t.duration_ms
ORDER BY ph.played_at DESC
LIMIT 100;
