# Host security boundaries

The host simulates movement, validates finite input, clamps movement input to unit length and enforces configured speeds. Physics knockback retains existing exceptions. Gun damage is fixed at 1, interval 0.22s, magazine 12 and reload 1.5s. Shot origins must be finite, within 2.5m and unobstructed from the host player anchor. Kick range is host physics based and cooldown is 0.8s. Manual respawn cooldown is 1s.

RPC budgets are per owned network object, use real time, allow bursts and silently discard excess requests. They do not kick or ban. Per-second rate / burst: fire 20/40, reload 8/16, kick 12/24, jump 20/40, respawn 4/8, nickname 1/4. Normal action cooldowns still apply after these inexpensive guards. NGO ownership checks apply to player ServerRpcs. Transport packet floods are outside these application-level guards.

Names are normalized on the host to 14 UTF-16 characters; markup delimiters, controls, bidi formatting and surrogate characters are removed. Room capacity remains 4, with connection-approval and post-connect checks. Connection payloads over 512 bytes are refused. Legacy table RPCs use the actual sender and check range; starting rounds is host-only.

Saves have size/count/finite-number/position/rotation bounds, GUID filenames, atomic replacement and one last-good .bak. Invalid primary files fall back to backup. A damaged primary is quarantined on the next successful write so it cannot replace a good backup. Deleting/clearing a room deletes its backup. Local records are marked local-unverified; this is metadata, not a cryptographic guarantee. There is no official ranking submission service in this project.

F6 stage-skip input/RPC was removed. ReleaseSecurityGuard rejects Development, debugger and profiler build flags, and checks selected text/key files under Assets for common private credentials without logging values. This is a limited prevention check, not a general secret scanner; keep all administrator credentials outside the project/client and rotate any previously shipped credentials. Public Unity project IDs are not administrator secrets. Tools > Security > Use Release Build Settings clears the current editor flags; every build is checked regardless.

Trust limitation: a host can modify its own executable/save/claimed player IDs. Verified competitive ranking and authenticated resume identities require a trusted backend with validated authentication tokens. No anti-cheat can make a player-owned host trustworthy. No official-ranking backend or third-party anti-cheat was added.

Validation: runtime C# and release guard compiled against installed Unity references with zero errors. Unity Editor pipeline was unavailable, so Unity RPC postprocessing, actual player builds and multiplayer/play-mode behavior remain unverified.
