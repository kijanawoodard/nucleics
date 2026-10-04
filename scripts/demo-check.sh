#!/usr/bin/env bash
# Spike 5 demo: proves `check` passes when clean and fails (exit 1) on a dangling link, a 404 page, a 500 page.
# Every temporary change is reverted at the end.
set -u
cd "$(dirname "$0")/.."
P=src/Nucleics.Web
run() { dotnet run --no-build --project $P -- "$@" 2>&1 | grep -v -E '^\s+(at |---)|^(fail|crit|warn|info)|Connection id|^\S*\[40m|^\s*$' ; echo "exit=${PIPESTATUS[0]}"; }
build() { dotnet build $P 2>&1 | grep -E ' error |Build succ' | sort -u; }
trap 'git checkout -- $P/Components/Pages/About.razor; rm -f $P/Components/Pages/Broken.razor $P/Components/Pages/Boom.razor; build >/dev/null' EXIT

build
echo "=== A) clean tree"; run check | grep -E '^\[|ERROR|LINK|FAIL|exit'
echo; echo "=== B) temporary broken link on /about"
sed -i 's#<p class="aside">Back to the#<p class="aside"><a href="/does-not-exist/">oops</a> Back to the#' $P/Components/Pages/About.razor
build; run check | grep -E '^\[|ERROR|LINK|FAIL|exit'
git checkout -- $P/Components/Pages/About.razor
echo; echo "=== C) temporary pages: /broken (NavigationManager.NotFound -> 404) and /boom (throws -> 500)"
printf '@page "/broken"\n@inject NavigationManager Nav\n@code { protected override void OnInitialized() => Nav.NotFound(); }\n' > $P/Components/Pages/Broken.razor
printf '@page "/boom"\n@code { protected override void OnInitialized() => throw new InvalidOperationException("boom"); }\n' > $P/Components/Pages/Boom.razor
build; run check | grep -E '^\[|ERROR|LINK|FAIL|exit'
rm -f $P/Components/Pages/Broken.razor $P/Components/Pages/Boom.razor
echo; echo "=== D) reverted"; build; run check | grep -E '^\[|ERROR|LINK|FAIL|exit'
echo; echo "=== E) auth filter: no auth-gated page exists in the site any more; the [Authorize]/[AllowAnonymous] skip logic is covered by tests/Kaizen.StaticSite.Tests (run: dotnet test tests/Kaizen.StaticSite.Tests)"
