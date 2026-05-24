-- Defence in depth: enforce at most one SystemAdmin at the schema level.
-- The bootstrap rule in UserRepository.CreateAsync uses SELECT EXISTS + INSERT
-- inside a READ COMMITTED transaction, which is vulnerable to concurrent
-- registrations both seeing "no users" and both becoming admin. This partial
-- unique index makes the second concurrent INSERT fail with SQLSTATE 23505.
CREATE UNIQUE INDEX ux_users_one_system_admin
    ON users ((is_system_admin))
    WHERE is_system_admin = true;
