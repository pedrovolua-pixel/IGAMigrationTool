# Portable source capture assertions

Run `dotnet run --project tests/unit/SyntheticEvaluationSourceIntegration.Tests -c Release --no-restore` after locked restore using the pinned .NET10.0.401 toolchain. The executable checks independent canonical JSON literals, raw owning default-encoder separation, exact SHA256, immutable detached/get-only output and deterministic semantic binding. It opens no database or provider. Unit internals friendship is limited to this assembly; the public library has no unchecked snapshot constructor/factory.
