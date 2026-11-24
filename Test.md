## Shared run contract
- Use the investigation date, timezone, and report path supplied by `core-ops`. If invoked directly, determine the date and local timezone once and use them consistently.
- Write only `results/YYYY-MM-DD/02.service-up-and-running.md`. return any qualifying critical evidence to `core-ops` and include it in this step report.
- Use read-only Datadog queries; do not modify dashboards, monitors, logs, or other Datadog resources.
- Distinguish observed facts from interpretation. Missing, truncated, or incomplete data is unverified, not a clean result.
- At completion, return `status` (`clear`, `issues_found`, or `unverified`), the report path, and critical findings (if any).

Reserve critical status for evidence of actual or imminent severe harm: a widespread outage, safety/security exposure, confirmed or credible data loss/corruption, or a material financial discrepancy with credible severe impact. A routine threshold miss, isolated anomaly, or ordinary follow-up is not critical by itself. When severity is unclear, state the uncertainty.

## Investigation
1. Load `dashboardId` from `configurations/core-ops.config.json` as described below and use `pup dashboards get $dashboardId --read-only` to inspect the [Service Health Overview widget](https://prospa-eng-p.datadoghq.com/dashboard/h89-2yg-f4a/servicing-dashboard?fromUser=true&index=*&refresh_mode=paused&tpl_var_Environment%5B0%5D=production&from_ts=1792386116994&to_ts=1792472516994&live=false&tile_focus=9100000000000000). Do not modify the dashboard or any Datadog resources.
2. Assess all services shown in the widget for health and unusual spikes or low numbers. Record the widget's time range and observed status; do not infer health from missing data.
3. If anything looks abnormal, investigate with read-only Datadog queries through `pup`. If a service is down or impact is significant, identify an affected Message ID from relevant telemetry and use `service-pulse` to search for its message information. Record the Message ID, message type, and relevant findings; distinguish observation from interpretation.
4. Write the result to `results/YYYY-MM-DD/02.service-up-and-running.md`. Include the dashboard and widget checked, time range, overall service status, abnormal observations and investigation evidence, and Message ID/ServiceControl findings when applicable. If no abnormalities are found, explicitly record that all observed services look healthy and there are no unusual spikes or low numbers.

For every dashboard request, run from the workspace root and load the config in the same PowerShell invocation as `pup`; PowerShell variables do not persist across separate execute invocations:

```powershell
$dashboardId = (Get-Content -Raw "configurations/core-ops.config.json" | ConvertFrom-Json).dashboardId
if ([string]::IsNullOrWhiteSpace($dashboardId)) {
    throw "dashboardId is missing from configurations/core-ops.config.json"
}
pup dashboards get $dashboardId --read-only
```
