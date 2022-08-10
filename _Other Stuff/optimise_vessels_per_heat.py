import argparse
import math
from typing import Tuple


def OptimiseVesselsPerHeat(count: int, limits: Tuple[int, int] = (6, 10)) -> Tuple[int, int]:
    options = reversed(list(range(limits[1]//2, limits[1] + 1))) if count > limits[1] and count < 2*limits[0]-1 else reversed(list(range(limits[0], limits[1] + 1)))
    for val in options:
        if count % val == 0:
            return val, 0
    result = OptimiseVesselsPerHeat(count + 1, limits)
    return result[0], result[1] + 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description="Optimise the number of vessels per heat.", formatter_class=argparse.ArgumentDefaultsHelpFormatter)
    parser.add_argument("count", type=int, help="The number of craft in the tournament.")
    parser.add_argument("-l", "--limits", type=int, nargs=2, default=(6, 10), help="Limits on the number of vessels.")
    args = parser.parse_args()

    result = OptimiseVesselsPerHeat(args.count, args.limits)
    full_heats = max(int(math.ceil(args.count/result[0]) - result[1]), 0)
    if full_heats > 0:
        string = f"Optimal is {full_heats} heats with {result[0]} vessels"
        if result[1] > 0:
            string += f" and {result[1]} heat{'s' if result[1]>1 else ''} with {result[0]-1} vessels"
    else:
        string = f"Optimal is a single heat with {args.count} vessels"
    print(string)
