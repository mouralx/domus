import type { State } from './models';

/** Unassigned cards use the first bucket and the first swimlane in their own project. */
export function assignDefaultPlacement(state: State): State {
  const swimlanes = [...state.swimlanes];
  if (swimlanes.length) {
    const first = swimlanes[0];
    for (const project of state.projects)
      if (!swimlanes.some((lane) => lane.project === project.id))
        swimlanes.push({ ...first, id: crypto.randomUUID(), project: project.id, complete: false });
  }
  return {
    ...state,
    swimlanes,
    tasks: state.tasks.map((task) => {
      const lanes = swimlanes.filter((lane) => lane.project === task.project);
      const bucket = state.buckets.some((item) => item.name === task.bucket)
        ? task.bucket
        : (state.buckets[0]?.name ?? '');
      const swimlane = lanes.some((item) => item.name === task.swimlane)
        ? task.swimlane
        : (lanes[0]?.name ?? '');
      return bucket === task.bucket && swimlane === task.swimlane
        ? task
        : { ...task, bucket, swimlane };
    }),
  };
}
