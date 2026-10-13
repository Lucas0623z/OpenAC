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
