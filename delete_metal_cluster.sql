-- Delete the Metal & Heavy cluster so you can re-save it with the correct description
DELETE FROM saved_clusters WHERE name LIKE '%Metal%';

-- Verify it's gone
SELECT COUNT(*) as remaining_clusters FROM saved_clusters;
