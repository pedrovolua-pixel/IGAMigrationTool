// Exercise the real approved workspace navigation without relaxing workflow checks.
export async function showAssessments(page) {
  const navigation = page.getByRole("navigation", {
    name: "Workspace sections",
    includeHidden: true,
  });
  await navigation.waitFor({ state: "attached" });
  if (!(await navigation.isVisible()))
    await page.getByRole("button", { name: "Menu", exact: true }).click();
  const target = navigation.getByRole("button", {
    name: "Assessments",
    exact: true,
  });
  await target.click();
  // Navigation moves focus on the next animation frame. Settle it before
  // a driver focuses a workflow control and sends keyboard input.
  await page.waitForFunction(
    () => document.activeElement?.id === "workspace-title",
  );
}
