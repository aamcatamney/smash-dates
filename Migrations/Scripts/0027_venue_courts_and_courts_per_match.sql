-- Split the conflated Venue "capacity" into two concepts:
--   * courts                — physical courts the hall has
--   * max_concurrent_matches — the Venue's own ceiling on simultaneous Matches (1 or 2)
-- and add a per-League courts_per_match rule. The number of Matches a Venue can host at
-- once in a slot is then min(max_concurrent_matches, floor(courts / courts_per_match)).
--
-- The old `capacity` already meant "simultaneous matches", so it becomes max_concurrent_matches
-- unchanged; courts is backfilled to capacity * 2 (the default courts_per_match) so existing
-- schedules keep the same slot capacity.

ALTER TABLE venues RENAME COLUMN capacity TO max_concurrent_matches;

ALTER TABLE venues ADD COLUMN courts integer NOT NULL DEFAULT 2 CHECK (courts >= 1);
UPDATE venues SET courts = max_concurrent_matches * 2;

-- Per-League: how many courts one Match occupies (rubbers run in parallel). Default 2.
ALTER TABLE leagues ADD COLUMN courts_per_match integer NOT NULL DEFAULT 2 CHECK (courts_per_match >= 1);
