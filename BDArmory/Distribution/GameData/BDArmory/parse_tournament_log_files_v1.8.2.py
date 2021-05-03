#!/usr/bin/env python3

# Standard library imports
import argparse
import json
import sys
from collections import Counter
from pathlib import Path

parser = argparse.ArgumentParser(description="Tournament log parser", formatter_class=argparse.ArgumentDefaultsHelpFormatter)
parser.add_argument('tournament', type=str, nargs='?', help="Tournament folder to parse.")
parser.add_argument('-q', '--quiet', action='store_true', help="Don't print results summary to console.")
parser.add_argument('-n', '--no-files', action='store_true', help="Don't create summary files.")
parser.add_argument('-s', '--score', action='store_false', help="Compute scores.")
parser.add_argument('-so', '--scores-only', action='store_true', help="Only display the scores in the summary on the console.")
parser.add_argument('-w', '--weights', type=str, default="1,0,-1.5,1,2e-3,3,1,5e-3,1e-5,0.5,0.01,1e-7,0,5e-2", help="Score weights (in order of main columns from 'Wins' to 'Ram').")
parser.add_argument('-c', '--current-dir', action='store_true', help="Parse the logs in the current directory as if it was a tournament without the folder structure.")
args = parser.parse_args()
args.score = args.score or args.scores_only

if args.current_dir:
	tournamentDir = Path('')
else:
	if args.tournament is None:
		tournamentDir = None
		logsDir = Path(__file__).parent / "Logs"
		if logsDir.exists():
			tournamentFolders = list(logsDir.resolve().glob("Tournament*"))
			if len(tournamentFolders) > 0:
				tournamentFolders = sorted(list(dir for dir in tournamentFolders if dir.is_dir()))
			if len(tournamentFolders) > 0:
				tournamentDir = tournamentFolders[-1]  # Latest tournament dir
		if tournamentDir is None:  # Didn't find a tournament dir, revert to current-dir
			tournamentDir = Path('')
			args.current_dir = True
	else:
		tournamentDir = Path(args.tournament)  # Specified tournament dir
tournamentData = {}

if args.score:
	try:
		weights = list(float(w) for w in args.weights.split(','))
	except:
		weights = []
	if len(weights) != 14:
		print('Invalid set of weights.')
		sys.exit()


def CalculateAccuracy(hits, shots): return 100 * hits / shots if shots > 0 else 0


for round in sorted(roundDir for roundDir in tournamentDir.iterdir() if roundDir.is_dir()) if not args.current_dir else (tournamentDir,):
	tournamentData[round.name] = {}
	for heat in sorted(round.glob("[0-9]*.log")):
		with open(heat, "r") as logFile:
			tournamentData[round.name][heat.name] = {'result': None, 'duration': 0, 'craft': {}}
			for line in logFile:
				line = line.strip()
				if 'BDArmory.BDACompetitionMode' not in line:
					continue  # Ignore irrelevant lines
				_, field = line.split(' ', 1)
				if field.startswith('Dumping Results'):
					tournamentData[round.name][heat.name]['duration'] = float(field[field.find('(') + 4:field.find(')') - 1])
				elif field.startswith('ALIVE:'):
					state, craft = field.split(':', 1)
					tournamentData[round.name][heat.name]['craft'][craft] = {'state': state}
				elif field.startswith('DEAD:'):
					state, order, time, craft = field.split(':', 3)
					tournamentData[round.name][heat.name]['craft'][craft] = {'state': state, 'deathOrder': int(order), 'deathTime': float(time)}
				elif field.startswith('MIA:'):
					state, craft = field.split(':', 1)
					tournamentData[round.name][heat.name]['craft'][craft] = {'state': state}
				elif field.startswith('WHOSHOTWHO:'):
					_, craft, shooters = field.split(':', 2)
					data = shooters.split(':')
					tournamentData[round.name][heat.name]['craft'][craft].update({'hitsBy': {player: int(hits) for player, hits in zip(data[1::2], data[::2])}})
				elif field.startswith('WHODAMAGEDWHOWITHBULLETS:'):
					_, craft, shooters = field.split(':', 2)
					data = shooters.split(':')
					tournamentData[round.name][heat.name]['craft'][craft].update({'bulletDamageBy': {player: float(damage) for player, damage in zip(data[1::2], data[::2])}})
				elif field.startswith('WHOHITWHOWITHMISSILES:'):
					_, craft, shooters = field.split(':', 2)
					data = shooters.split(':')
					tournamentData[round.name][heat.name]['craft'][craft].update({'missileHitsBy': {player: int(hits) for player, hits in zip(data[1::2], data[::2])}})
				elif field.startswith('WHOPARTSHITWHOWITHMISSILES:'):
					_, craft, shooters = field.split(':', 2)
					data = shooters.split(':')
					tournamentData[round.name][heat.name]['craft'][craft].update({'missilePartsHitBy': {player: int(hits) for player, hits in zip(data[1::2], data[::2])}})
				elif field.startswith('WHODAMAGEDWHOWITHMISSILES:'):
					_, craft, shooters = field.split(':', 2)
					data = shooters.split(':')
					tournamentData[round.name][heat.name]['craft'][craft].update({'missileDamageBy': {player: float(damage) for player, damage in zip(data[1::2], data[::2])}})
				elif field.startswith('WHORAMMEDWHO:'):
					_, craft, rammers = field.split(':', 2)
					data = rammers.split(':')
					tournamentData[round.name][heat.name]['craft'][craft].update({'rammedPartsLostBy': {player: int(partsLost) for player, partsLost in zip(data[1::2], data[::2])}})
				elif field.startswith('CLEANKILL:'):
					_, craft, killer = field.split(':', 2)
					tournamentData[round.name][heat.name]['craft'][craft].update({'cleanKillBy': killer})
				elif field.startswith('CLEANMISSILEKILL:'):
					_, craft, killer = field.split(':', 2)
					tournamentData[round.name][heat.name]['craft'][craft].update({'cleanMissileKillBy': killer})
				elif field.startswith('CLEANRAM:'):
					_, craft, killer = field.split(':', 2)
					tournamentData[round.name][heat.name]['craft'][craft].update({'cleanRamKillBy': killer})
				elif field.startswith('OTHERKILL'):
					_, craft, reason = field.split(':', 2)
					tournamentData[round.name][heat.name]['craft'][craft].update({'otherKillReason': reason})
				elif field.startswith('ACCURACY:'):
					_, craft, accuracy = field.split(':', 2)
					hits, shots = accuracy.split('/')
					accuracy = CalculateAccuracy(int(hits), int(shots))
					tournamentData[round.name][heat.name]['craft'][craft].update({'accuracy': accuracy, 'hits': int(hits), 'shots': int(shots)})
				elif field.startswith('RESULT:'):
					heat_result = field.split(':', 2)
					result_type = heat_result[1]
					if (len(heat_result) > 2):
						teams = json.loads(heat_result[2])
						if isinstance(teams, dict):  # Win, single team
							tournamentData[round.name][heat.name]['result'] = {'result': result_type, 'teams': {teams['team']: ', '.join(teams['members'])}}
						elif isinstance(teams, list):  # Draw, multiple teams
							tournamentData[round.name][heat.name]['result'] = {'result': result_type, 'teams': {team['team']: ', '.join(team['members']) for team in teams}}
					else:  # Mutual Annihilation
						tournamentData[round.name][heat.name]['result'] = {'result': result_type}
				elif field.startswith('DEADTEAMS:'):
					dead_teams = json.loads(field.split(':', 1)[1])
					if len(dead_teams) > 0:
						tournamentData[round.name][heat.name]['result'].update({'dead teams': {team['team']: ', '.join(team['members']) for team in dead_teams}})
				# Ignore Tag mode for now.

if not args.no_files:
	with open(tournamentDir / 'results.json', 'w') as outFile:
		json.dump(tournamentData, outFile, indent=2)


craftNames = sorted(list(set(craft for round in tournamentData.values() for heat in round.values() for craft in heat['craft'].keys())))
teamWins = Counter([team for round in tournamentData.values() for heat in round.values() if heat['result']['result'] == "Win" for team in heat['result']['teams']])
teamDraws = Counter([team for round in tournamentData.values() for heat in round.values() if heat['result']['result'] == "Draw" for team in heat['result']['teams']])
teamDeaths = Counter([team for round in tournamentData.values() for heat in round.values() for team in heat['result']['dead teams']])
teams = {team: members for round in tournamentData.values() for heat in round.values() if 'teams' in heat['result'] for team, members in heat['result']['teams'].items()}
summary = {
	'craft': {
		craft: {
			'wins': len([1 for round in tournamentData.values() for heat in round.values() if heat['result']['result'] == "Win" and craft in next(iter(heat['result']['teams'].values())).split(", ")]),
			'survivedCount': len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'ALIVE']),
			'deathCount': (
				len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD']),  # Total
				len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanKillBy' in heat['craft'][craft]]),  # Bullets
				len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanMissileKillBy' in heat['craft'][craft]]),  # Missiles
				len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanRamKillBy' in heat['craft'][craft]]),  # Rams
				len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and not any(field in heat['craft'][craft] for field in ('cleanKillBy', 'cleanMissileKillBy', 'cleanRamKillBy')) and any(field in heat['craft'][craft] for field in ('hitsBy', 'missilePartsHitBy', 'rammedPartsLostBy'))]),  # Dirty kill
				len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and not any(field in heat['craft'][craft] for field in ('hitsBy', 'missilePartsHitBy', 'rammedPartsLostBy')) and not any('rammedPartsLostBy' in data and craft in data['rammedPartsLostBy'] for data in heat['craft'].values())]),  # Suicide (died without being hit or ramming anyone).
			),
			'deathOrder': sum([heat['craft'][craft]['deathOrder'] / len(heat['craft']) if 'deathOrder' in heat['craft'][craft] else 1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft']]),
			'deathTime': sum([heat['craft'][craft]['deathTime'] if 'deathTime' in heat['craft'][craft] else heat['duration'] for round in tournamentData.values() for heat in round.values() if craft in heat['craft']]),
			'cleanKills': (
				len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if any((field in data and data[field] == craft) for field in ('cleanKillBy', 'cleanMissileKillBy', 'cleanRamKillBy'))]),  # Total
				len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if 'cleanKillBy' in data and data['cleanKillBy'] == craft]),  # Bullets
				len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if 'cleanMissileKillBy' in data and data['cleanMissileKillBy'] == craft]),  # Missiles
				len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if 'cleanRamKillBy' in data and data['cleanRamKillBy'] == craft]),  # Rams
			),
			'assists': len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if data['state'] == 'DEAD' and any(field in data and craft in data[field] for field in ('hitsBy', 'missilePartsHitBy', 'rammedPartsLostBy')) and not any((field in data and data[field] == craft) for field in ('cleanKillBy', 'cleanMissileKillBy', 'cleanRamKillBy'))]),
			'hits': sum([heat['craft'][craft]['hits'] for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'hits' in heat['craft'][craft]]),
			'bulletDamage': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('bulletDamageBy',) if field in data and craft in data[field]]),
			'missileHits': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('missileHitsBy',) if field in data and craft in data[field]]),
			'missileHitsTaken': sum([sum(heat['craft'][craft]['missileHitsBy'].values()) for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'missileHitsBy' in heat['craft'][craft]]),
			'missilePartsHit': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('missilePartsHitBy',) if field in data and craft in data[field]]),
			'missileDamage': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('missileDamageBy',) if field in data and craft in data[field]]),
			'ramScore': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('rammedPartsLostBy',) if field in data and craft in data[field]]),
			'accuracy': CalculateAccuracy(sum([heat['craft'][craft]['hits'] for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'hits' in heat['craft'][craft]]), sum([heat['craft'][craft]['shots'] for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'shots' in heat['craft'][craft]])),
		}
		for craft in craftNames
	},
	'team results': {
		'wins': teamWins,
		'draws': teamDraws,
		'deaths': teamDeaths
	},
	'teams': teams
}

for craft in summary['craft'].values():
	spawns = craft['survivedCount'] + craft['deathCount'][0]
	craft.update({
		'damage/hit': craft['bulletDamage'] / craft['hits'] if craft['hits'] > 0 else 0,
		'hits/spawn': craft['hits'] / spawns if spawns > 0 else 0,
		'damage/spawn': craft['bulletDamage'] / spawns if spawns > 0 else 0,
	})

if args.score:
	for craft in summary['craft'].values():
		craft.update({
			'score':
			weights[0] * craft['wins'] +
			weights[1] * craft['survivedCount'] +
			weights[2] * craft['deathCount'][0] +
			weights[3] * craft['deathOrder'] +
			weights[4] * craft['deathTime'] +
			weights[5] * craft['cleanKills'][0] +
			weights[6] * craft['assists'] +
			weights[7] * craft['hits'] +
			weights[8] * craft['bulletDamage'] +
			weights[9] * craft['missileHits'] +
			weights[10] * craft['missilePartsHit'] +
			weights[11] * craft['missileDamage'] +
			weights[12] * craft['missileHitsTaken'] +
			weights[13] * craft['ramScore']
		})

if not args.no_files:
	with open(tournamentDir / 'summary.json', 'w') as outFile:
		json.dump(summary, outFile, indent=2)

if len(summary['craft']) > 0:
	if not args.no_files:
		csv_summary = "craft," + ",".join(
			",".join(('deathCount', 'dcB', 'dcM', 'dcR', 'dcA', 'dcS')) if k == 'deathCount' else
			",".join(('cleanKills', 'ckB', 'ckM', 'ckR')) if k == 'cleanKills' else
			k for k in next(iter(summary['craft'].values())).keys()) + "\n"
		csv_summary += "\n".join(craft + "," + ",".join(str(int(100 * v) / 100) if not isinstance(v, tuple) else ",".join(str(int(100 * sf) / 100) for sf in v) for v in scores.values()) for craft, scores in summary['craft'].items())
		with open(tournamentDir / 'summary.csv', 'w') as outFile:
			outFile.write(csv_summary)

	if not args.quiet:
		# Write results to console
		strings = []
		headers = ['Name', 'Wins', 'Survive', 'Deaths (BMRAS)', 'D.Order', 'D.Time', 'Kills (BMR)', 'Assists', 'Hits', 'Damage', 'MisHits', 'MisParts', 'MisDmg', 'HitByMis', 'Ram', 'Acc%', 'Dmg/Hit', 'Hits/Sp', 'Dmg/Sp'] if not args.scores_only else ['Name']
		if args.score:
			headers.insert(1, 'Score')
		summary_strings = {'header': {field: field for field in headers}}
		for craft in sorted(summary['craft']):
			tmp = summary['craft'][craft]
			spawns = tmp['survivedCount'] + tmp['deathCount'][0]
			summary_strings.update({
				craft: {
					'Name': craft,
					'Wins': f"{tmp['wins']}",
					'Survive': f"{tmp['survivedCount']}",
					'Deaths (BMRAS)': f"{tmp['deathCount'][0]} ({' '.join(str(s) for s in tmp['deathCount'][1:])})",
					'D.Order': f"{tmp['deathOrder']:.3f}",
					'D.Time': f"{tmp['deathTime']:.1f}",
					'Kills (BMR)': f"{tmp['cleanKills'][0]} ({' '.join(str(s) for s in tmp['cleanKills'][1:])})",
					'Assists': f"{tmp['assists']}",
					'Hits': f"{tmp['hits']}",
					'Damage': f"{tmp['bulletDamage']:.0f}",
					'MisHits': f"{tmp['missileHits']}",
					'MisParts': f"{tmp['missilePartsHit']}",
					'MisDmg': f"{tmp['missileDamage']:.0f}",
					'HitByMis': f"{tmp['missileHitsTaken']}",
					'Ram': f"{tmp['ramScore']}",
					'Acc%': f"{tmp['accuracy']:.2f}",
					'Dmg/Hit': f"{tmp['damage/hit']:.1f}",
					'Hits/Sp': f"{tmp['hits/spawn']:.1f}",
					'Dmg/Sp': f"{tmp['damage/spawn']:.1f}"
				}
			})
			if args.score:
				summary_strings[craft]['Score'] = f"{tmp['score']:.3f}"
		columns_to_show = [header for header in headers if not all(craft[header] == "0" for craft in list(summary_strings.values())[1:])]
		column_widths = {column: max(len(craft[column]) + 2 for craft in summary_strings.values()) for column in headers}
		strings.append(''.join(f"{header:{column_widths[header]}s}" for header in columns_to_show))
		for craft in sorted(summary['craft'], key=None if not args.score else lambda craft: summary['craft'][craft]['score'], reverse=False if not args.score else True):
			strings.append(''.join(f"{summary_strings[craft][header]:{column_widths[header]}s}" for header in columns_to_show))

		teamNames = sorted(list(set([team for result_type in summary['team results'].values() for team in result_type])))
		default_team_names = [chr(k) for k in range(ord('A'), ord('A') + len(summary['craft']))]
		if len(teamNames) > 0 and not all(name in default_team_names for name in teamNames):  # Don't do teams if they're assigned as 'A', 'B', ... as they won't be consistent between rounds.
			name_length = max([len(team) for team in teamNames])
			strings.append(f"\nTeam{' '*(name_length-4)}\tWins\tDraws\tDeaths\tVessels")
			for team in teamNames:
				strings.append(f"{team}{' '*(name_length-len(team))}\t{teamWins[team]}\t{teamDraws[team]}\t{teamDeaths[team]}\t{summary['teams'][team]}")
		for string in strings:
			print(string)
else:
	print("No valid log files found.")
