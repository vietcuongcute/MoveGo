export type Route =
  | { name: 'vehicle'; id: number }
  | { name: 'order'; code: string };

export type Tab = 'vehicles' | 'lookup' | 'settings';

export type Nav = {
  push: (r: Route) => void;
  pop: () => void;
  goTab: (t: Tab) => void;
};
