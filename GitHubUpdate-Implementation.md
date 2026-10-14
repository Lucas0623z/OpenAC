# OpenAC updates

The update-library navigation replaces the old ASUS settings/update-centre page
at the user's request. Clicking **检查更新** reads only published releases from
`https://api.github.com/repos/Lucas0623z/OpenAC/releases`; it does not query ASUS
component updates. Current packages share one OpenAC release version across the
GUI and device adapters. This does not identify arbitrary Windows driver updates.

The public request needs no token, credentials, or installation changes.
The release list is paginated in bounded requests; a preview installation can
discover newer previews and stable versions. A stable installation accepts stable
releases. Semantic version precedence is used, rather than list order or lexical
sorting. Drafts, invalid versions, missing publication dates, and release URLs
outside the fixed project are rejected. An empty/incomplete/failed response is an
error, never a successful 'up to date' result.

The installer must be named `OpenAC-<release version>-Setup.exe` and its URL must
match that release under the fixed GitHub project. An optional GitHub SHA-256
digest is parsed without claiming download verification. Download/update buttons
open the published GitHub asset/release only after the user selects them; this
implementation does not silently install or execute downloads. Official model
BIOS/driver catalogues belong to the corresponding device page.

Reference: [GitHub Releases REST API](https://docs.github.com/en/rest/releases/releases).
51 pure checks cover SemVer/channel ordering, publication state, foreign URLs,
asset identity, incomplete results, and revalidation of cached links at restore
and click time. A real unauthenticated request on
2026-10-13 returned `v0.3.0-preview` and its existing setup asset with the recorded
release digest. No installer or hardware action was run by these checks.
