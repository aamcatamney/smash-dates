ALTER TABLE users
    ADD COLUMN is_system_admin boolean NOT NULL DEFAULT false;
