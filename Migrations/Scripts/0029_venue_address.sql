-- Optional free-text address on a venue, so the UI can link it to a map provider
-- (a Google Maps search URL today). Display/navigation aid only — the scheduler ignores it.
ALTER TABLE venues ADD COLUMN address text NULL;
