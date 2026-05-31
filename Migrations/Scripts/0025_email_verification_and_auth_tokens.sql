-- Email verification: existing users are grandfathered verified (DEFAULT true); new sign-ups
-- are inserted as false by UserRepository.CreateAsync (the bootstrap SystemAdmin excepted).
ALTER TABLE users ADD COLUMN email_verified boolean NOT NULL DEFAULT true;

-- One-time, expiring tokens for password reset and email verification. Only the SHA-256 hash
-- of the token is stored; the raw token travels in the emailed link.
CREATE TABLE auth_tokens (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    purpose     text NOT NULL CHECK (purpose IN ('PasswordReset', 'EmailVerification')),
    token_hash  text NOT NULL,
    expires_at  timestamptz NOT NULL,
    used_at     timestamptz NULL,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_auth_tokens_hash ON auth_tokens (token_hash);
CREATE INDEX ix_auth_tokens_user ON auth_tokens (user_id);
