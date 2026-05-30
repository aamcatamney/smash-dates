ALTER TABLE matches ADD COLUMN home_score  integer NULL;
ALTER TABLE matches ADD COLUMN away_score  integer NULL;
ALTER TABLE matches ADD COLUMN played_on   date NULL;
ALTER TABLE matches ADD COLUMN is_walkover boolean NOT NULL DEFAULT false;
