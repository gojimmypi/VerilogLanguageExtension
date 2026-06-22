#!/usr/bin/env python3
"""Compare VerilogLanguageExtension editor snapshot exports.

This tool compares deterministic JSON emitted by SnapshotExporter and checks
small targeted expectations such as "dout has hover text". It intentionally
ignores machine-specific fields such as absolute paths, assembly versions, and
snapshot version numbers.
"""

from __future__ import annotations

import argparse
import difflib
import json
import shutil
import sys
from pathlib import Path
from typing import Any, Dict, Iterable, List, Tuple


Snapshot = Dict[str, Any]
FailureList = List[str]


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8-sig") as f:
        return json.load(f)


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as f:
        json.dump(value, f, indent=4, sort_keys=True)
        f.write("\n")


def iter_snapshot_files(root: Path) -> Iterable[Path]:
    if not root.exists():
        return []
    return sorted(root.rglob("*.snapshot.json"))


def normalize_slashes(value: str) -> str:
    return value.replace("\\", "/")


def snapshot_key(snapshot_path: Path, snapshot_root: Path, snapshot: Snapshot) -> str:
    file_relative_path = snapshot.get("FileRelativePath") or snapshot.get("FilePath") or snapshot_path.name
    file_relative_path = normalize_slashes(str(file_relative_path))

    run_name = snapshot.get("RunName") or snapshot_path.parent.name
    base_name = snapshot_path.name

    # Preserve sequence prefixes like 0001-issue10.v.snapshot.json because repeated-open
    # scenarios are meaningful state-leak tests.
    return normalize_slashes(str(Path(str(run_name)) / base_name))


def normalize_span(span: Dict[str, Any], include_types: bool) -> Dict[str, Any]:
    normalized: Dict[str, Any] = {
        "Line": span.get("Line"),
        "Column": span.get("Column"),
        "Text": span.get("Text", ""),
    }

    if include_types:
        normalized["Types"] = sorted(span.get("Types") or [])
    else:
        normalized["TagDetail"] = span.get("TagDetail", "")
        hover = span.get("HoverText")
        if hover:
            normalized["HoverText"] = hover

    return normalized


def normalize_token(token: Dict[str, Any]) -> Dict[str, Any]:
    normalized = {
        "Line": token.get("Line"),
        "Column": token.get("Column"),
        "Text": token.get("Text", ""),
        "Context": token.get("Context", ""),
    }
    return normalized


def normalize_symbol(symbol: Dict[str, Any]) -> Dict[str, Any]:
    normalized = {
        "Scope": symbol.get("Scope", ""),
        "Name": symbol.get("Name", ""),
        "TokenType": symbol.get("TokenType", ""),
    }

    hover = symbol.get("HoverText")
    if hover:
        normalized["HoverText"] = hover

    return normalized


def normalize_snapshot(snapshot: Snapshot) -> Dict[str, Any]:
    normalized = {
        "SchemaVersion": snapshot.get("SchemaVersion"),
        "RunName": snapshot.get("RunName", ""),
        "FileRelativePath": normalize_slashes(str(snapshot.get("FileRelativePath") or snapshot.get("FilePath") or "")),
        "ContentType": snapshot.get("ContentType", ""),
        "TextSha256": snapshot.get("TextSha256", ""),
        "Errors": snapshot.get("Errors") or [],
        "Classifications": [normalize_span(item, True) for item in snapshot.get("Classifications") or []],
        "Tags": [normalize_span(item, False) for item in snapshot.get("Tags") or []],
        "Tokens": [normalize_token(item) for item in snapshot.get("Tokens") or []],
        "Symbols": [normalize_symbol(item) for item in snapshot.get("Symbols") or []],
    }

    normalized["Classifications"].sort(key=lambda item: (item.get("Line") or 0, item.get("Column") or 0, item.get("Text") or "", str(item.get("Types") or "")))
    normalized["Tags"].sort(key=lambda item: (item.get("Line") or 0, item.get("Column") or 0, item.get("Text") or "", item.get("TagDetail") or ""))
    normalized["Tokens"].sort(key=lambda item: (item.get("Line") or 0, item.get("Column") or 0, item.get("Text") or "", item.get("Context") or ""))
    normalized["Symbols"].sort(key=lambda item: (item.get("Scope") or "", item.get("Name") or "", item.get("TokenType") or ""))

    return normalized


def load_snapshots(root: Path) -> Dict[str, Tuple[Path, Snapshot, Dict[str, Any]]]:
    result: Dict[str, Tuple[Path, Snapshot, Dict[str, Any]]] = {}
    for path in iter_snapshot_files(root):
        snapshot = load_json(path)
        key = snapshot_key(path, root, snapshot)
        result[key] = (path, snapshot, normalize_snapshot(snapshot))
    return result


def compare_snapshots(current_root: Path, baseline_root: Path, failures: FailureList) -> None:
    current = load_snapshots(current_root)
    baseline = load_snapshots(baseline_root)

    if not current:
        failures.append(f"No current snapshots found in {current_root}")
        return

    if not baseline:
        failures.append(f"No baseline snapshots found in {baseline_root}")
        return

    current_keys = set(current)
    baseline_keys = set(baseline)

    for key in sorted(current_keys - baseline_keys):
        failures.append(f"Baseline missing snapshot: {key}")

    for key in sorted(baseline_keys - current_keys):
        failures.append(f"Current run missing snapshot: {key}")

    for key in sorted(current_keys & baseline_keys):
        current_path, _, current_norm = current[key]
        baseline_path, _, baseline_norm = baseline[key]

        if current_norm != baseline_norm:
            current_text = json.dumps(current_norm, indent=4, sort_keys=True).splitlines()
            baseline_text = json.dumps(baseline_norm, indent=4, sort_keys=True).splitlines()
            diff = "\n".join(difflib.unified_diff(
                baseline_text,
                current_text,
                fromfile=str(baseline_path),
                tofile=str(current_path),
                lineterm=""))
            failures.append(f"Snapshot differs: {key}\n{diff}")


def update_baseline(current_root: Path, baseline_root: Path) -> None:
    if baseline_root.exists():
        shutil.rmtree(baseline_root)
    baseline_root.mkdir(parents=True, exist_ok=True)

    for src in iter_snapshot_files(current_root):
        rel = src.relative_to(current_root)
        dst = baseline_root / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)


def matching_snapshots_for_file(current: Dict[str, Tuple[Path, Snapshot, Dict[str, Any]]], expected_file: str) -> List[Tuple[Path, Snapshot, Dict[str, Any]]]:
    expected_file = normalize_slashes(expected_file).lower()
    expected_name = Path(expected_file).name.lower()
    matches: List[Tuple[Path, Snapshot, Dict[str, Any]]] = []

    for path, snapshot, normalized in current.values():
        rel = normalize_slashes(str(normalized.get("FileRelativePath", ""))).lower()
        full = normalize_slashes(str(snapshot.get("FilePath", ""))).lower()
        base = Path(rel or full or path.name).name.lower()

        if rel.endswith(expected_file) or full.endswith(expected_file) or base == expected_name:
            matches.append((path, snapshot, normalized))

    return matches


def text_contains_all(haystack: str, needles: Any) -> bool:
    if isinstance(needles, str):
        needles = [needles]
    if needles is None:
        needles = []
    haystack_lower = (haystack or "").lower()
    return all(str(needle).lower() in haystack_lower for needle in needles)


def all_text_candidates(normalized: Dict[str, Any]) -> List[Dict[str, Any]]:
    candidates: List[Dict[str, Any]] = []

    for item in normalized.get("Tags") or []:
        candidate = dict(item)
        candidate["Source"] = "Tags"
        candidates.append(candidate)

    for item in normalized.get("Classifications") or []:
        candidate = dict(item)
        candidate["Source"] = "Classifications"
        candidates.append(candidate)

    for item in normalized.get("Tokens") or []:
        candidate = dict(item)
        candidate["Source"] = "Tokens"
        candidates.append(candidate)

    for item in normalized.get("Symbols") or []:
        candidate = {
            "Text": item.get("Name", ""),
            "HoverText": item.get("HoverText", ""),
            "TagDetail": item.get("TokenType", ""),
            "Source": "Symbols",
        }
        candidates.append(candidate)

    return candidates


def check_required_text(path: Path, normalized: Dict[str, Any], required: Dict[str, Any], failures: FailureList) -> None:
    text = required.get("Text")
    if not text:
        return

    candidates = [item for item in all_text_candidates(normalized) if item.get("Text") == text]
    if not candidates:
        failures.append(f"{path}: missing text {text}")
        return

    hover_contains = required.get("HoverContains")
    if hover_contains is not None and not any(text_contains_all(item.get("HoverText", ""), hover_contains) for item in candidates):
        hover_values = [
            {
                "Source": item.get("Source", ""),
                "HoverText": item.get("HoverText", ""),
                "TagDetail": item.get("TagDetail", ""),
            }
            for item in candidates
        ]
        failures.append(f"{path}: text {text} hover did not contain {hover_contains}; actual={hover_values}")


def check_expectation(expectation_path: Path, current: Dict[str, Tuple[Path, Snapshot, Dict[str, Any]]], failures: FailureList) -> None:
    expectation = load_json(expectation_path)
    expected_file = expectation.get("File")
    if not expected_file:
        failures.append(f"Expectation missing File: {expectation_path}")
        return

    matches = matching_snapshots_for_file(current, expected_file)
    if not matches:
        failures.append(f"Expectation file had no current snapshots: {expected_file}")
        return

    for path, _snapshot, normalized in matches:
        errors = normalized.get("Errors") or []
        if errors:
            failures.append(f"{path}: exporter recorded errors: {errors}")

        symbols = normalized.get("Symbols") or []

        for required in expectation.get("MustHaveSymbols") or []:
            name = required.get("Name")
            if not name:
                continue

            candidates = [item for item in symbols if item.get("Name") == name]
            if not candidates:
                failures.append(f"{path}: missing symbol {name}")
                continue

            hover_contains = required.get("HoverContains")
            if hover_contains is not None and not any(text_contains_all(item.get("HoverText", ""), hover_contains) for item in candidates):
                hover_values = [item.get("HoverText", "") for item in candidates]
                failures.append(f"{path}: symbol {name} hover did not contain {hover_contains}; actual={hover_values}")

        for forbidden in expectation.get("MustNotHaveSymbols") or []:
            found = [item for item in symbols if item.get("Name") == forbidden]
            if found:
                failures.append(f"{path}: forbidden symbol exists: {forbidden}")

        # Backward-compatible name: this now means "the expected text must be
        # visible in exported editor data". The text may come from tags,
        # classifications, parser tokens, or symbols. When HoverContains is set,
        # the check still requires an exported hover source, usually Tags or Symbols.
        for required in expectation.get("MustHaveTaggedText") or []:
            check_required_text(path, normalized, required, failures)

        for required in expectation.get("MustHaveText") or []:
            check_required_text(path, normalized, required, failures)


def check_expectations(current_root: Path, expectations_root: Path, failures: FailureList) -> None:
    current = load_snapshots(current_root)
    if not expectations_root.exists():
        failures.append(f"Expectations directory not found: {expectations_root}")
        return

    for expectation_path in sorted(expectations_root.glob("*.expect.json")):
        check_expectation(expectation_path, current, failures)


def main(argv: List[str]) -> int:
    parser = argparse.ArgumentParser(description="Compare VerilogLanguageExtension snapshot exports.")
    parser.add_argument("--current", required=True, type=Path, help="Current snapshot directory")
    parser.add_argument("--baseline", type=Path, help="Baseline snapshot directory")
    parser.add_argument("--expectations", type=Path, help="Expectation JSON directory")
    parser.add_argument("--update-baseline", action="store_true", help="Replace baseline with current snapshots")
    args = parser.parse_args(argv)

    failures: FailureList = []

    if args.update_baseline:
        if not args.baseline:
            print("--update-baseline requires --baseline", file=sys.stderr)
            return 2
        update_baseline(args.current, args.baseline)
        print(f"Updated baseline: {args.baseline}")
        return 0

    if args.baseline:
        compare_snapshots(args.current, args.baseline, failures)

    if args.expectations:
        check_expectations(args.current, args.expectations, failures)

    if failures:
        print("Snapshot regression check failed:")
        for index, failure in enumerate(failures, start=1):
            print(f"\n[{index}] {failure}")
        return 1

    print("Snapshot regression check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
