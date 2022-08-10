#!/usr/bin/env python3

# Standard library imports
import argparse
import json
import sys
from collections import Counter
from pathlib import Path

VERSION = "1.0"

parser = argparse.ArgumentParser(description="Parse results.json of a N-choose-K style tournament producing a table of who-beat-who.", formatter_class=argparse.ArgumentDefaultsHelpFormatter, epilog="Note: this also works on FFA style tournaments, but may not be meaningful.")
parser.add_argument('results', type=str, nargs='?', help="results.json file to parse.")
parser.add_argument('-o', '--output', default="n-choose-k.csv", help="File to output CSV to.")
parser.add_argument("--version", action='store_true', help="Show the script version, then exit.")
args = parser.parse_args()

if args.version:
    print(f"Version: {VERSION}")
    sys.exit()

if args.results is None:
    logsDir = Path(__file__).parent / "Logs"
    if logsDir.exists():
        tournamentFolders = list(logsDir.resolve().glob("Tournament*"))
        if len(tournamentFolders) > 0:
            tournamentFolders = sorted(list(dir for dir in tournamentFolders if dir.is_dir()))
        if len(tournamentFolders) > 0:
            args.results = tournamentFolders[-1] / "results.json"  # Results in latest tournament dir


if args.results is not None:
    results_file = Path(args.results)
    if not results_file.exists():
        print(f"File not found: {results_file}")
    else:
        with open(results_file, 'r') as f:
            data = json.load(f)
        counts = Counter([(next(iter(heat['result']['teams'].keys())), next(iter(heat['result']['dead teams'].keys()))) for Round in data.values() for heat in Round.values() if heat['result']['result'] == 'Win'])
        A = set(k[0] for k in counts.keys())
        B = set(k[1] for k in counts.keys())
        names = sorted(A.union(B))
        name_map = {n: c for c, n in enumerate(names)}
        t = [[0] * len(names) for i in range(len(names))]
        for k, c in counts.items():
            t[name_map[k[0]]][name_map[k[1]]] = c
        with open(args.output, 'w') as f:
            f.write("vs," + ",".join(names) + ",,sum(wins)\n")
            for i, name in enumerate(names):
                f.write(name + "," + ",".join([str(c) for c in t[i]]) + ",," + str(sum(t[i])) + "\n")
            f.write(","*(len(names)+2))
            f.write("\nsum(losses)," + ",".join([str(sum(t[i][j] for i in range(len(names)))) for j in range(len(names))])+",,\n")
