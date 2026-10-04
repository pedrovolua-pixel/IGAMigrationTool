// Exercise the real approved workspace navigation without relaxing workflow checks.
export async function showAssessments(page) {
  const target = page
    .getByRole("navigation", { name: "Workspace sections" })
    .getByRole("button", { name: "Assessments", exact: true });
  await target.waitFor({ state: "attached" });
  if (!(await target.isVisible()))
    await page.getByRole("button", { name: "Menu", exact: true }).click();
  await target.click();
  // Navigation moves focus on the next animation frame. Settle it before
  // a driver focuses a workflow control and sends keyboard input.
  await page.waitForFunction(
    () => document.activeElement?.id === "workspace-title",
  );
}
