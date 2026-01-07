-- Verify the saved_clusters table structure
SELECT column_name, data_type, character_maximum_length
FROM information_schema.columns
WHERE table_name = 'saved_clusters'
ORDER BY ordinal_position;
