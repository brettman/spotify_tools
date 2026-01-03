-- Check what was actually saved
SELECT id, name, 
       length(genres) as genres_length,
       array_length(string_to_array(genres, ','), 1) as genre_count,
       genres
FROM saved_clusters
WHERE name LIKE '%Metal%'
ORDER BY created_at DESC
LIMIT 1;
