# Managed API reference

DocFX generates this reference from the solution's intentional managed contract and reusable library
surfaces.

<!--#if (Sample) -->
The generated configuration selects the reference modules' abstractions, domain libraries, and reusable
extension projects automatically.
<!--#else -->
This minimal scaffold has no intentional managed contract project, so its DocFX configuration omits metadata
extraction and keeps strict documentation builds warning-free. When the application adds a public contract or
reusable library, add that project to the `metadata` selection in `docs/docfx/docfx.json`; API documentation is
then validated as part of every documentation build.
<!--#endif -->

Deployable and module implementation projects, Web and native clients, persistence projects, migrations,
and tests are excluded. Their public CLR types are composition or implementation details rather than
supported application contracts. A minimal scaffold therefore has this policy page but no generated type
pages.

The repository documentation entrypoint validates this reference together with the authored documentation.
Managed-reference YAML files and the metadata manifest generated under `api/reference/` are build output and
must not be committed.
