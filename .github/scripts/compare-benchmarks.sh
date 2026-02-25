#!/usr/bin/env bash
# Compare BenchmarkDotNet results against committed baselines.
# Usage: bash compare-benchmarks.sh <baselines-dir> <results-dir>
# Output: Markdown tables suitable for GitHub step summary.
# Exit code: always 0 (informational only).

set -euo pipefail

BASELINES_DIR="${1:?Usage: compare-benchmarks.sh <baselines-dir> <results-dir>}"
RESULTS_DIR="${2:?Usage: compare-benchmarks.sh <baselines-dir> <results-dir>}"

TOTAL=0
REGRESSIONS=0

pct_change() {
  local base="$1" new="$2"
  if [[ "$base" == "0" || "$base" == "null" || -z "$base" ]]; then
    echo "N/A"
    return
  fi
  awk "BEGIN { printf \"%.1f\", (($new - $base) / $base) * 100 }"
}

for baseline_file in "$BASELINES_DIR"/*-report-full-compressed.json; do
  [[ -f "$baseline_file" ]] || continue

  # Extract class name from filename
  # e.g. DynamoDb.ExpressionMapping.Benchmarks.Benchmarks.ProjectionBuilderBenchmarks-report-full-compressed.json
  filename="$(basename "$baseline_file")"
  class_name="${filename%-report-full-compressed.json}"

  # Find matching results file (BenchmarkDotNet appends a timestamp)
  results_file=""
  for candidate in "$RESULTS_DIR"/${class_name}*-report-full-compressed.json; do
    if [[ -f "$candidate" ]]; then
      results_file="$candidate"
      break
    fi
  done

  if [[ -z "$results_file" ]]; then
    echo "### ${class_name##*.}"
    echo ""
    echo "_No matching results file found — skipped._"
    echo ""
    continue
  fi

  # Short display name (last segment of fully-qualified class)
  display_name="${class_name##*.}"

  echo "### ${display_name}"
  echo ""
  echo "| Method | Parameters | Baseline Mean (ns) | New Mean (ns) | Time Δ% | Baseline Alloc (B) | New Alloc (B) | Alloc Δ% | Status |"
  echo "|--------|------------|-------------------:|-------------:|--------:|------------------:|-------------:|---------:|--------|"

  # Read baseline benchmarks as TSV lines: Method\tParameters\tMean\tAlloc\tGen0
  while IFS=$'\t' read -r method params base_mean base_alloc base_gen0; do
    TOTAL=$((TOTAL + 1))

    # Look up same method+params in results
    new_data=$(jq -r --arg m "$method" --arg p "$params" \
      '.Benchmarks[] | select(.Method == $m and .Parameters == $p) | [.Statistics.Mean, .Memory.BytesAllocatedPerOperation, .Memory.Gen0Collections] | @tsv' \
      "$results_file" 2>/dev/null || true)

    if [[ -z "$new_data" ]]; then
      echo "| ${method} | ${params:-—} | ${base_mean} | — | — | ${base_alloc} | — | — | ❓ missing |"
      continue
    fi

    IFS=$'\t' read -r new_mean new_alloc new_gen0 <<< "$new_data"

    time_pct=$(pct_change "$base_mean" "$new_mean")
    alloc_pct=$(pct_change "$base_alloc" "$new_alloc")

    status="✅"

    # Check regression thresholds
    if [[ "$time_pct" != "N/A" ]]; then
      regression=$(awk "BEGIN { print ($time_pct > 20) ? 1 : 0 }")
      if [[ "$regression" == "1" ]]; then
        status="⚠️ time"
        REGRESSIONS=$((REGRESSIONS + 1))
      fi
    fi

    if [[ "$alloc_pct" != "N/A" ]]; then
      regression=$(awk "BEGIN { print ($alloc_pct > 50) ? 1 : 0 }")
      if [[ "$regression" == "1" ]]; then
        [[ "$status" == "✅" ]] && status="⚠️ alloc" || status="${status}+alloc"
        REGRESSIONS=$((REGRESSIONS + 1))
      fi
    fi

    # Gen0: any increase is a regression
    if [[ "$base_gen0" != "null" && "$new_gen0" != "null" && -n "$base_gen0" && -n "$new_gen0" ]]; then
      gen0_increase=$(awk "BEGIN { print ($new_gen0 > $base_gen0) ? 1 : 0 }")
      if [[ "$gen0_increase" == "1" ]]; then
        [[ "$status" == "✅" ]] && status="⚠️ gen0" || status="${status}+gen0"
        REGRESSIONS=$((REGRESSIONS + 1))
      fi
    fi

    # Format numbers for display
    base_mean_fmt=$(awk "BEGIN { printf \"%.1f\", $base_mean }")
    new_mean_fmt=$(awk "BEGIN { printf \"%.1f\", $new_mean }")

    echo "| ${method} | ${params:-—} | ${base_mean_fmt} | ${new_mean_fmt} | ${time_pct}% | ${base_alloc:-0} | ${new_alloc:-0} | ${alloc_pct}% | ${status} |"

  done < <(jq -r '.Benchmarks[] | [.Method, .Parameters, .Statistics.Mean, .Memory.BytesAllocatedPerOperation, .Memory.Gen0Collections] | @tsv' "$baseline_file")

  echo ""
done

echo "---"
echo ""
echo "**Summary:** ${TOTAL} benchmarks compared, ${REGRESSIONS} regression(s) flagged."
echo ""
echo "Thresholds: Mean time >20%, Allocation >50%, Gen0 GC any increase."

exit 0
