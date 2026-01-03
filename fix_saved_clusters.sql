-- Fix saved_clusters table to use proper snake_case columns
-- Safe to run - only affects the saved_clusters table

-- Drop the incorrectly created table
DROP TABLE IF EXISTS saved_clusters CASCADE;

-- Remove old migration entries from history
DELETE FROM "__EFMigrationsHistory" 
WHERE "MigrationId" LIKE '%AddSavedClustersTable%';

-- That's it! Now run: dotnet ef database update
