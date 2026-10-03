# Cycle07 synthetic fixture secret classification

The accepted-schema proof and readiness fixtures contain SHA256 logical and root-cause identities derived from fictional inputs. The author independently recomputes one logical identity and two root-group identities in portable assertions; the independent literal consumer verifies the five exact native identities. They are public fixture content, not credentials.

The root .gitleaks.toml extends the default Gitleaks 8.30.1 rules. Its single AND allowlist applies only to generic-api-key, the five named new fixture files (including their build copies), and seven anchored exact detector matches containing those five identities. It does not suppress arbitrary values, entire files, other paths or other credential rules. Native raw redacted observations, derivation assertions, exact public matches, configuration hashes and negative controls are retained under work/m08-cycle07-evidence/coordinator.

Executed controls confirm the exact fixture match is allowed, a different fictional value in the same path is detected, the same public value in another path is detected, and a fictional GitHub-pattern value in the permitted path is still detected by github-pat. The initial raw detector observations remain evidence; they are not erased by classification.

Configuration semantics are defined by the pinned [upstream 8.30.1 configuration](https://github.com/gitleaks/gitleaks/blob/v8.30.1/config/config.go). This changes scanner classification of verified synthetic literals only; it changes no runtime policy, credential handling or dependency version.
