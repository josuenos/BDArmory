#!/usr/bin/env python3

# Standard library imports
import json
from pathlib import Path

# Third party imports
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D


def plot(paths, colours):
    """ Plot vessel traces using matplotlib.

    Args:
        paths (Path): The file paths of the vessels to trace.
        colours (str): The colours of each trace.
    """
    fig = plt.figure()
    ax = fig.add_subplot(111, projection='3d')
    for path, colour in zip(paths, colours):
        with open(path, 'r') as f:
            m = json.load(f)
        p = [m['position'] for m in m[1:]]
        x = [p[0] for p in p]
        z = [p[1] for p in p]
        y = [p[2] for p in p]
        ax.scatter(x, y, z, c=colour, marker='.')
    plt.show()


paths = [p for p in (Path(__file__).parent / 'Logs' / 'VesselTraces').iterdir() if p.suffix == '.json']
colours = 'rgbcmyk' * (len(paths) // 8 + 1)  # Loop colours if there's too many paths.
plot(paths, colours[:len(paths)])
