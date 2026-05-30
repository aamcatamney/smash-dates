-- Per-League soft-penalty configuration for the scheduler's 2-opt optimisation.
-- Defaults match the engine's out-of-the-box constants. target_gap_days NULL means
-- "derive ~half the season span".
ALTER TABLE leagues ADD COLUMN spread_weight   integer NOT NULL DEFAULT 2 CHECK (spread_weight >= 0);
ALTER TABLE leagues ADD COLUMN leg_weight      integer NOT NULL DEFAULT 1 CHECK (leg_weight >= 0);
ALTER TABLE leagues ADD COLUMN min_gap_days    integer NOT NULL DEFAULT 7 CHECK (min_gap_days >= 0);
ALTER TABLE leagues ADD COLUMN target_gap_days integer NULL CHECK (target_gap_days IS NULL OR target_gap_days >= 0);
