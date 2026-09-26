# Feature specifications

Create one directory per substantial feature using `NNN-feature-name`. Its canonical artifact set is:

- `product-spec.md`
- `technical-spec.md`
- `implementation-plan.md`
- `test-plan.md`
- `status.md`

Copy the matching files from [`templates`](./templates/) and keep requirement and acceptance-criterion identifiers stable. Product approval precedes technical design; technical approval precedes implementation. A human owner grants approvals.

Small, low-risk bug fixes may use a lighter record when expected behavior, root cause, regression evidence, and verification remain traceable.
