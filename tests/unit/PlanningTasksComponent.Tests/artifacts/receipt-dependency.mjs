import { useEffect, useRef, useState } from "file:///private/tmp/iga-cycle14-task-ui/src/web/node_modules/react/index.js";
import { DemoRequestError, request } from "./inert-api.mjs";
const emptyDraft = () => ({
	reason: "",
	pending: null,
	busy: false,
	error: null,
	requiresRefresh: false
});
function closed(v, keys) {
	return v !== null && typeof v === "object" && !Array.isArray(v) && Object.keys(v).length === keys.length && keys.every((k) => Object.hasOwn(v, k));
}
function timestamp(v) {
	if (typeof v !== "string" || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$/.test(v) || !Number.isFinite(Date.parse(v))) return false;
	return new Date(v).toISOString() === v.replace(/(?:\.(\d{1,7}))?Z$/, (_, f) => "." + (f ?? "").padEnd(3, "0").slice(0, 3) + "Z");
}
export function planningTaskReceiptAgrees(value, runId, taskId, actorId, command) {
	if (!closed(value, [
		"schemaVersion",
		"demoOnly",
		"issue",
		"alreadyApplied",
		"receipt",
		"alreadyExistsTaskId"
	]) || value.schemaVersion !== 1 || value.demoOnly !== true || value.issue !== null || typeof value.alreadyApplied !== "boolean") return false;
	if (value.alreadyExistsTaskId !== null) return command.kind === "Create" && command.expectedRevision === 0 && value.alreadyExistsTaskId === taskId && value.receipt === null && value.alreadyApplied === false;
	if (!closed(value.receipt, [
		"schemaVersion",
		"eventId",
		"runId",
		"taskId",
		"kind",
		"revision",
		"actorId",
		"recordedAtUtc",
		"sourceDigest"
	])) return false;
	const r = value.receipt;
	return r.schemaVersion === "synthetic-planning-task-receipt-v1" && r.eventId === command.eventId && r.runId === runId && r.taskId === taskId && r.kind === command.kind && r.revision === command.expectedRevision + 1 && Number.isSafeInteger(r.revision) && r.actorId === actorId && r.sourceDigest === command.expectedSourceDigest && timestamp(r.recordedAtUtc);
}
function validReason(s) {
	if (s.length > 2e3 || /^\p{White_Space}*$/u.test(s)) return false;
	for (let i = 0; i < s.length; i++) {
		const c = s.charCodeAt(i);
		if (c >= 55296 && c <= 56319) {
			const n = s.charCodeAt(++i);
			if (!(n >= 56320 && n <= 57343)) return false;
		} else if (c >= 56320 && c <= 57343) return false;
	}
	return true;
}
/** Parent-owned drafts survive analysis child unmounts. A receipt always causes a whole-source reread. */
export function usePlanningTasks(run, csrfToken, analysis, readEpoch, reload, retainedDrafts) {
	const [saved, setSaved] = useState(() => retainedDrafts.current);
	const savedRef = useRef(saved);
	savedRef.current = saved;
	retainedDrafts.current = saved;
	const currentRun = useRef(run.runId);
	currentRun.current = run.runId;
	const operations = useRef(new Map());
	const generation = useRef(0);
	const key = (taskId) => `${run.runId}/${taskId}`;
	const change = (draftKey, patch) => setSaved((values) => ({
		...values,
		[draftKey]: {
			...values[draftKey] ?? emptyDraft(),
			...patch
		}
	}));
	useEffect(() => {
		++generation.current;
		for (const operation of operations.current.values()) operation.controller.abort();
		operations.current.clear();
		setSaved((values) => Object.fromEntries(Object.entries(values).map(([draftKey, draft]) => [draftKey, draft.busy ? {
			...draft,
			busy: false,
			error: "The response is uncertain. Refresh the source or explicitly retry the same event.",
			requiresRefresh: true
		} : draft])));
		return () => {
			retainedDrafts.current = Object.fromEntries(Object.entries(savedRef.current).map(([draftKey, draft]) => [draftKey, draft.busy ? {
				...draft,
				busy: false,
				error: "The response is uncertain. Refresh the source or explicitly retry the same event.",
				requiresRefresh: true
			} : draft]));
			++generation.current;
			for (const operation of operations.current.values()) operation.controller.abort();
			operations.current.clear();
		};
	}, [run.runId, run.revision]);
	async function send(taskId, command) {
		const selectedRun = run.runId, draftKey = key(taskId), actorId = analysis?.planningTasks?.actorId;
		if (!actorId || operations.current.has(draftKey)) return;
		const controller = new AbortController(), epoch = generation.current, sourceEpoch = readEpoch.current;
		operations.current.set(draftKey, {
			controller,
			epoch
		});
		change(draftKey, {
			pending: command,
			busy: true,
			error: null
		});
		const current = () => !controller.signal.aborted && currentRun.current === selectedRun && generation.current === epoch;
		try {
			const result = await request(`/runs/${encodeURIComponent(selectedRun)}/planning-tasks/${encodeURIComponent(taskId)}/events`, controller.signal, command, csrfToken);
			if (!current()) return;
			if (readEpoch.current !== sourceEpoch || !planningTaskReceiptAgrees(result, selectedRun, taskId, actorId, command)) throw new Error("Unverifiable response");
			change(draftKey, {
				pending: null,
				busy: false,
				error: null,
				requiresRefresh: false
			});
			reload(result.alreadyExistsTaskId ? "This recommendation already has a planning task. Its current source and history were refreshed." : result.alreadyApplied ? "The original historical receipt was returned. Current source and history were refreshed." : "Planning task event saved. Current source and history were refreshed.", taskId);
		} catch (caught) {
			if (!current()) return;
			if (caught instanceof DemoRequestError && caught.status < 500) {
				change(draftKey, {
					pending: null,
					busy: false,
					requiresRefresh: true,
					error: `${caught.message} Inspect and refresh the source before a new action.`
				});
			} else {
				change(draftKey, {
					pending: command,
					busy: false,
					requiresRefresh: false,
					error: "The response was lost or could not be verified. The event may already be saved. Explicitly retry the same event or refresh the source."
				});
			}
			reload("The task response could not be applied. Saved source and history were reread.", taskId);
		} finally {
			if (operations.current.get(draftKey)?.controller === controller) operations.current.delete(draftKey);
		}
	}
	return {
		drafts: Object.fromEntries(Object.entries(saved).filter(([k]) => k.startsWith(`${run.runId}/`)).map(([k, v]) => [k.slice(run.runId.length + 1), v])),
		onReasonChange: (taskId, reason) => {
			const d = saved[key(taskId)];
			if (!d?.busy && !d?.pending) change(key(taskId), { reason });
		},
		onAction: (taskId, kind) => {
			const detail = analysis?.planningTasks, option = detail?.options.find((o) => o.identity.taskId === taskId), entry = detail?.entries.find((e) => e.identity.taskId === taskId), draft = saved[key(taskId)] ?? emptyDraft();
			if (detail?.status !== "Ready" || !detail.source || !option || draft.busy || draft.pending || draft.requiresRefresh) return;
			const allowed = kind === "Create" ? option.canCreate && !entry : entry && {
				ReconfirmPlan: entry.canReconfirm,
				StartProgress: entry.canStart,
				ReturnToPlanned: entry.canReturnToPlanned,
				Complete: entry.canComplete,
				Cancel: entry.canCancel,
				Reopen: entry.canReopen,
				Comment: entry.canComment
			}[kind];
			if (!allowed) return;
			if (!validReason(draft.reason)) {
				change(key(taskId), { error: "Enter a nonblank reason or comment of at most 2,000 characters." });
				requestAnimationFrame(() => document.getElementById("planning-task-error-" + taskId)?.focus());
				return;
			}
			const command = Object.freeze({
				eventId: crypto.randomUUID(),
				kind,
				expectedRevision: kind === "Create" ? 0 : entry.revision,
				expectedSourceDigest: detail.source.artifactSource.sourceDigest,
				expectedAttestations: Object.freeze(option.currentAttestations.map((a) => Object.freeze({ ...a }))),
				reason: draft.reason
			});
			void send(taskId, command);
		},
		onRetry: (taskId) => {
			const d = saved[key(taskId)];
			if (d?.pending && !d.busy) void send(taskId, d.pending);
		},
		onRefresh: (taskId) => {
			if (saved[key(taskId)]?.busy) return;
			change(key(taskId), { requiresRefresh: false });
			reload("Inspect the refreshed recommendation and task before another action.", taskId);
		}
	};
}
