-- Records why an async schedule generation failed, so the UI can surface it after the season
-- falls back to Draft. Cleared when generation is (re)started. See ScheduleRunner.
ALTER TABLE seasons ADD COLUMN scheduling_error text NULL;
