#!/usr/bin/env python3
"""Summarize executed NUnit evidence; never infer manual verification from tests."""
import json
import pathlib
import sys
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
REQUIRED = {
    'FixtureRulesTests': 'Boundaries, goals, restarts and match phases',
    'OffsideRulesTests': 'Offside position and exemptions',
    'OffsideIntegrationTests': 'Offside involvement and live restarts',
    'FoulRulesTests': 'Foul and sanction tables',
    'RefereeIntegrationTests': 'Advantage, cards, keeper offences and penalties',
    'ParticipationTests': 'Injuries, substitutions and minimum players',
    'GoalkeeperTests': 'Keeper handling and response choices',
    'SetPieceTests': 'Set-piece roles and deliveries',
    'MatchPlaybackTests': 'Full/highlight pacing and protected stops',
    'FootballVerificationTests': 'Created-career role and consequence matrix',
    'CharacterCreationTests': 'All selectable roles and starting budgets',
    'CareerWeekTests': 'Career fixture lifecycle',
    'DecisionPresentationTests': 'Possession decision presentation',
    'DefensePresentationTests': 'Defensive decision presentation',
    'GoalkeeperPresentationTests': 'Keeper decision presentation',
    'FootballActionPresentationTests': 'Football action presentation',
}


def main():
    xml = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / 'TestResults/football.xml'
    root = ET.parse(xml).getroot()
    cases = list(root.iter('test-case'))
    rows, errors = [], []
    for fixture, label in REQUIRED.items():
        selected = [c for c in cases if c.get('classname', '').endswith('.' + fixture)]
        passed = sum(c.get('result') == 'Passed' for c in selected)
        if not selected or passed != len(selected):
            errors.append(f'{fixture}: {passed}/{len(selected)} passed')
        rows.append(f'| {label} | {passed}/{len(selected)} | `{fixture}` |')
    roles = [p['id'] for p in json.loads((ROOT / 'Assets/FootballRpg/Runtime/Content/Resources/PrototypeFixture.json').read_text())['positions']]
    records = {}
    for case in cases:
        if not case.get('classname', '').endswith('.FootballVerificationTests'):
            continue
        for line in (case.findtext('output') or '').splitlines():
            if line.startswith('FOOTBALL_POSITION_EVIDENCE '):
                value = json.loads(line.split(' ', 1)[1])
                if case.get('result') != 'Passed' or value['role'] in records:
                    errors.append('Invalid or duplicate role evidence: ' + value['role'])
                records[value['role']] = value
    if set(records) != set(roles):
        errors.append('Executed role matrix does not cover the complete content position catalogue.')
    if root.get('result') != 'Passed' or any(c.get('result') != 'Passed' for c in cases):
        errors.append('The test run has failures, skips or inconclusive results.')
    report = ['# Football verification evidence', '',
              f"Run: {root.get('start-time')} — {root.get('end-time')}",
              f"Automated status: {'FAIL' if errors else 'PASS'}; {root.get('passed')}/{root.get('total')} tests.",
              'Manual status: NOT ATTESTED by this report. See docs/football-verification.md and the manual matrix.', '',
              '| Area | Passed/executed | Suite |', '|---|---:|---|', *rows, '',
              '| Role | Decisions | Keeper decisions | User actions | Dismissals | End | Full/highlight state |',
              '|---|---:|---:|---:|---:|---|---|']
    for role in roles:
        r = records.get(role)
        if r:
            report.append(f"| {role.upper()} | {r['decisions']} | {r['keeperDecisions']} | {r['actions']} | {r['dismissals']} | {'Abandoned (<7)' if r['abandoned'] else 'Full time'} | `{r['digest']}` |")
        else:
            report.append(f'| {role.upper()} | — | — | — | — | Missing | NOT VERIFIED |')
    abandoned = sum(r['abandoned'] for r in records.values())
    report += ['', f'Balance observation: {abandoned}/{len(records)} role scenarios ended early after dismissals. This is visible tuning evidence, not proof of balanced match pacing.',
               'Each role uses a balanced created character, the attended authored week, seed 42 and two 600-second halves. Random injuries and coach substitutions are disabled only in this paired role replay; separate named scenarios exercise those systems.',
               'Both modes must have identical stops/probabilities and complete serialized career state. These automated presentation calls are not mouse or visual verification.']
    if errors:
        report += ['', '## Failures', *['- ' + error for error in errors]]
    target = xml.with_suffix('.md')
    target.write_text('\n'.join(report) + '\n')
    xml.with_suffix('.json').write_text(json.dumps({'automatedPassed': not errors, 'manualVerified': False, 'errors': errors, 'positions': records}, indent=2) + '\n')
    print(f"Football verification: {'FAIL' if errors else 'PASS'}; {root.get('passed')}/{root.get('total')} tests, {len(records)}/{len(roles)} roles. Evidence: {target}")
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())
