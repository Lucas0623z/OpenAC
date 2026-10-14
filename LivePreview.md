# Local live preview

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\preview.ps1` after
building the adapters with `build-all.ps1`. This opens the desktop application.
The preview host watches the explicit GUI source list in `build.ps1` and refreshes
after a save has settled for 900 ms. It keeps its process running and replaces the
WPF window with the newly compiled version, retaining the page, device tab,
window bounds, scroll offset, macro/scenario drafts, and observed device values.
This is automatic window refresh; it does not patch methods inside an existing
WPF window. Unsaved fields not held in a page's draft model and undo history are
not guaranteed to survive.

The previously working window remains visible if compilation or constructing
the next page fails. The host delays replacement while a device request, macro,
scenario, update query, running Aura animation, or modal dialog is active.
Refreshing does not reapply hardware configuration or resume input playback.
The hardware-control permission is reset in each new window.

Adapters run from a separate `.preview/runtime-<generation>` folder, so rebuilding the normal adapter outputs
does not conflict with a live worker's executable lock. Changed adapter binaries
also cause a refresh; a complete new generation is prepared before closing the
working window. Its copies are used after the old connections close.
Adapter source changes still require `build-all.ps1`; GUI source changes compile
automatically. The development host is not included in installer runtime files.

`.preview/status.json` records the process, successful refresh count, current page,
and errors. Compiler details go to `.preview/build.log`. All preview outputs and
these observations are excluded from Git and source packages. `preview.ps1
-VerifyOnly` runs an unshown state-transfer check with no hardware operations.
The actual running preview was also tested with a deliberately failing GUI
compile: the same process/page survived, and the next successful compile refreshed
it. The new host was also tested with a temporarily unavailable adapter output:
preparing the next runtime failed while the old window remained visible; restoring
the output refreshed the same process and page. Stop/restart the development host
for changes to `LivePreviewHost.cs` itself. Its private resume snapshot preserves
the page and tabs across such a host restart; cached update links are revalidated.
.NET Framework retains loaded assemblies until the host exits, so long preview
sessions can use more memory; restarting the preview releases those assemblies.

This phase also preserves Aura snapshots/selections and Windows power/display
drafts as data. Applying settings rechecks their real current state. A running
monitor read delays replacement; expired readings are hidden while waiting.
Windows restore points belong to their live controller instance: preview waits
until the user explicitly keeps or restores that change. Closing does not
automatically restore settings.

Battery queries share a System semaphore in the stable AppDomain, including
between byte-loaded assemblies. Caller timeout does not release an unfinished
provider query. Battery snapshots expire after 30 seconds and ordinary monitor
readings after 15 seconds. Developer-only `.preview/assets.json` can point
`productImageRoot` at existing official PNGs; that path and the images are private.

The host now stages seven workers, including NotebookWorker. `-StartInactive`
opens the restored preview without asking WPF to activate it. Changed host code
still requires a normal host restart. A single-instance mutex prevents two live
hosts from editing the shared preview state.

Window geometry is saved as physical pixel dimensions with a layout version.
Old snapshots migrate from their authored logical dimensions once; repeated
reloads use absolute dimensions and never compound the DPI correction. Only
OpenAC's own root receives an inverse scale. The current 150% desktop DPI was
observed through the real preview window; 100/125/150/200% and repeated restores
also have 2054 unshown size and startup-phase checks. Startup/DPI-transition snapshots preserve the restored physical target until layout settles. Fixed page headers remain separate from the
scrolling body, while overflowing device tabs scroll horizontally.

Keyboard OLED/lever, mouse performance, and power-setting drafts travel as data
only. Active applies delay replacement; restored drafts must still match the
device and supported range. Hardware permission is never restored. Notebook
queries are manual, single-batch, bounded and quarantined on timeout; the expiry
timer only removes stale display values. Cached firmware checks and headset
subpages also survive replacement without starting queries or updates.

LCD view mode, built-in preset, background and information count survive a
replacement. Its sensor selections and cached LCD capabilities do not: opaque
sensor IDs are rebound only after a fresh read from the new worker. Applying a
monitor draft checks that same live process session inside the request queue;
an expired session never silently restarts a worker to send the old IDs. The
same-page rendering of an active monitor draft also pauses automatic rereads;
returning to LCD from another view or losing the session permits a fresh read.
Mouse buttons, static lighting and Zone editing now have data-only draft models.
Their selection, color, independent X/Y values and calibration expansion states
survive replacement. Restored observations cannot permit an apply until a fresh
read replaces the old cache. All gear SET requests use the precise current
snapshot and a live worker-session token inside the request queue; an expired
read cannot restart another worker to send its draft. Tab-specific read keys keep
same-page redraws from replacing current dirty drafts, while returning to a tab
or losing its process still permits a fresh read. No control permission or active
apply is transferred.

Worker requests use explicit UTF-8 bytes. Their shared input reader handles only
known stream-start preambles from Framework process startup, then decodes every
payload as strict UTF-8. It leaves later BOM characters intact and rejects
invalid or truncated data without changing a global console encoding. Memory
fixtures and mock-process requests cover the first-frame failure previously
seen during the full regression run.

The settings popup keeps single-field actions separate; its unimplemented aggregate
Save, warning and sleeping-wallpaper controls remain disabled.
