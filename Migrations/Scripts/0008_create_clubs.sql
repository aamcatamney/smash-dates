CREATE TABLE clubs (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name          text NOT NULL,
    short_code    text NOT NULL,
    contact_email text NOT NULL,
    notes         text NULL,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    CHECK (char_length(short_code) BETWEEN 3 AND 5),
    CHECK (short_code = upper(short_code)),
    CHECK (short_code ~ '^[A-Z0-9]+$')
);

CREATE UNIQUE INDEX ux_clubs_name_lower ON clubs (lower(name));
CREATE UNIQUE INDEX ux_clubs_short_code ON clubs (short_code);
