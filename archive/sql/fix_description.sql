-- Fix the description for the Metal & Heavy cluster
-- This will regenerate the description from the actual genres in the database
UPDATE saved_clusters
SET description = 
    CASE 
        WHEN array_length(string_to_array(genres, ','), 1) <= 5 
        THEN 'Includes: ' || genres
        ELSE 'Includes: ' || array_to_string((string_to_array(genres, ','))[1:5], ', ') || 
             ' (+' || (array_length(string_to_array(genres, ','), 1) - 5)::text || ' more)'
    END,
    updated_at = NOW()
WHERE name LIKE '%Metal%';

-- Verify the fix
SELECT name, description, 
       array_length(string_to_array(genres, ','), 1) as genre_count
FROM saved_clusters
WHERE name LIKE '%Metal%';
