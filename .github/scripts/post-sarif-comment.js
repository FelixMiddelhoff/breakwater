// Reads a SARIF file produced by `dotnet build -p:ErrorLog=<path>;version=2`,
// filters findings to the files this PR actually touched, and posts (or
// updates) a single summarizing PR comment. Idempotent across pushes: it
// looks for an existing comment carrying MARKER and edits it in place
// instead of creating a new one every run.
//
// Invoked from .github/workflows/pr-review.yml via actions/github-script:
//   const script = require(process.env.GITHUB_WORKSPACE + '/.github/scripts/post-sarif-comment.js');
//   await script({ github, context, core });
//
// Expects these environment variables to be set by the workflow:
//   SARIF_PATH     - path to the SARIF file (may not exist if the build produced none)
//   CHANGED_FILES  - newline-separated list of files changed in the PR, repo-root-relative
//   DOCS_BASE_URL  - base URL for rule docs, e.g. https://github.com/OWNER/REPO/blob/main/docs/rules
//
// SARIF level -> breakwater tier, for display only (severity as reported by
// the analyzer's own DiagnosticDescriptor, not re-derived here):
//   "error"   -> Warning-tier finding (most severe breakwater ships)
//   "warning" -> Warning-tier finding
//   "note"    -> Suggestion-tier finding
const fs = require("fs");
const path = require("path");

const MARKER = "<!-- breakwater-report -->";

function levelLabel(level) {
  if (level === "error" || level === "warning") return "Warning";
  if (level === "note") return "Suggestion";
  return level || "Info";
}

function severityRank(level) {
  if (level === "error") return 0;
  if (level === "warning") return 1;
  if (level === "note") return 2;
  return 3;
}

function readSarifResults(sarifPath) {
  if (!sarifPath || !fs.existsSync(sarifPath)) return [];
  const raw = fs.readFileSync(sarifPath, "utf8");
  let doc;
  try {
    doc = JSON.parse(raw);
  } catch (err) {
    throw new Error(`Failed to parse SARIF file at ${sarifPath}: ${err.message}`);
  }
  const results = [];
  for (const run of doc.runs || []) {
    for (const result of run.results || []) {
      const loc = (result.locations || [])[0];
      const physical = loc && loc.physicalLocation;
      const uri = physical && physical.artifactLocation && physical.artifactLocation.uri;
      const region = physical && physical.region;
      const line = region && (region.startLine || region.endLine);
      results.push({
        ruleId: result.ruleId || "BW???",
        level: result.level || "warning",
        message: (result.message && result.message.text) || "",
        file: uri ? decodeURIComponent(uri) : null,
        line: line || null,
      });
    }
  }
  return results;
}

function normalizePath(p) {
  if (!p) return p;
  return p.replace(/\\/g, "/").replace(/^\.\//, "");
}

function stripFileUri(uri) {
  if (!uri) return uri;
  let p = uri.startsWith("file://") ? uri.slice("file://".length) : uri;
  // file:///D:/... on Windows leaves a leading slash before the drive letter.
  p = p.replace(/^\/([A-Za-z]:)/, "$1");
  return p;
}

function filterToChangedFiles(results, changedFiles) {
  if (!changedFiles || changedFiles.length === 0) return [];
  const changedList = changedFiles.map(normalizePath);
  const bwOnly = results.filter((r) => /^BW\d+$/.test(r.ruleId));
  const matched = [];
  for (const r of bwOnly) {
    if (!r.file) continue;
    const norm = normalizePath(stripFileUri(r.file));
    // SARIF artifact URIs may be absolute or repo-relative; match on suffix
    // so either form lines up with the changed-files list from `git diff`,
    // and display the short repo-relative path instead of the raw URI.
    const hit = changedList.find((changed) => norm === changed || norm.endsWith("/" + changed));
    if (hit) matched.push({ ...r, file: hit });
  }
  return matched;
}

function buildCommentBody(results, docsBaseUrl) {
  if (results.length === 0) {
    return `${MARKER}\n### Breakwater: no issues found\n\nNo migration-safety findings on the files this PR changes.`;
  }

  const sorted = [...results].sort((a, b) => severityRank(a.level) - severityRank(b.level));

  const counts = {};
  for (const r of sorted) {
    const label = levelLabel(r.level);
    counts[label] = (counts[label] || 0) + 1;
  }
  const countSummary = Object.entries(counts)
    .map(([label, n]) => `${n} ${label}`)
    .join(", ");

  const rows = sorted
    .map((r) => {
      const loc = r.file ? `${r.file}${r.line ? `:${r.line}` : ""}` : "unknown location";
      const ruleLink = docsBaseUrl ? `[${r.ruleId}](${docsBaseUrl}/${r.ruleId}.md)` : r.ruleId;
      const message = r.message.replace(/\|/g, "\\|").replace(/\r?\n/g, " ");
      return `| ${ruleLink} | ${levelLabel(r.level)} | \`${loc}\` | ${message} |`;
    })
    .join("\n");

  return [
    MARKER,
    `### Breakwater: ${results.length} finding${results.length === 1 ? "" : "s"} (${countSummary})`,
    "",
    "| Rule | Severity | Location | Message |",
    "| --- | --- | --- | --- |",
    rows,
    "",
    "_Posted by the breakwater PR review workflow. Findings are also visible as inline build annotations._",
  ].join("\n");
}

async function findExistingComment(github, context) {
  const { owner, repo } = context.repo;
  const issue_number = context.payload.pull_request.number;
  const perPage = 100;
  let page = 1;
  while (true) {
    const { data: comments } = await github.rest.issues.listComments({
      owner,
      repo,
      issue_number,
      per_page: perPage,
      page,
    });
    const found = comments.find((c) => c.body && c.body.includes(MARKER));
    if (found) return found;
    if (comments.length < perPage) return null;
    page += 1;
  }
}

module.exports = async ({ github, context, core }) => {
  if (!context.payload.pull_request) {
    core.info("Not a pull_request event; skipping SARIF comment step.");
    return;
  }

  const sarifPath = process.env.SARIF_PATH;
  const changedFilesRaw = process.env.CHANGED_FILES || "";
  const changedFiles = changedFilesRaw.split("\n").map((s) => s.trim()).filter(Boolean);
  const docsBaseUrl = process.env.DOCS_BASE_URL || "";

  let allResults;
  try {
    allResults = readSarifResults(sarifPath);
  } catch (err) {
    core.setFailed(err.message);
    return;
  }

  const relevant = filterToChangedFiles(allResults, changedFiles);

  core.info(`SARIF results total=${allResults.length} relevant-to-PR=${relevant.length}`);

  const existing = await findExistingComment(github, context);

  if (relevant.length === 0) {
    if (!existing) {
      core.info("No findings and no prior comment; skipping (no comment posted).");
      return;
    }
    const body = buildCommentBody([], docsBaseUrl);
    await github.rest.issues.updateComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      comment_id: existing.id,
      body,
    });
    core.info(`Updated existing comment ${existing.id} to "no issues found".`);
    return;
  }

  const body = buildCommentBody(relevant, docsBaseUrl);
  if (existing) {
    await github.rest.issues.updateComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      comment_id: existing.id,
      body,
    });
    core.info(`Updated existing comment ${existing.id} with ${relevant.length} findings.`);
  } else {
    const { data: created } = await github.rest.issues.createComment({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: context.payload.pull_request.number,
      body,
    });
    core.info(`Created comment ${created.id} with ${relevant.length} findings.`);
  }
};

// Exported for standalone/unit testing outside the Actions runtime.
module.exports._internal = {
  readSarifResults,
  filterToChangedFiles,
  buildCommentBody,
  normalizePath,
  stripFileUri,
  levelLabel,
};
