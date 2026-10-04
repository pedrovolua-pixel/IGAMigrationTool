# Portable accepted-schema checks

Run the executable with the pinned .NET SDK. Full proof/readiness JSON, bundle/reference and hash fixtures were produced independently before implementation. The representative readiness fixture declares its old run observation explicitly; it is a fixed canonical test input, not a current database capture. Tests cover exact bytes, 23 bindings, old field preservation, culture, detached metadata and concrete scope/reference/provenance counterexamples.
