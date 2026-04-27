import {
  ResponsiveContainer,
  BarChart,
  Bar,
  CartesianGrid,
  XAxis,
  YAxis,
  Tooltip,
} from "recharts";

type MonthlyTrendItem = {
  label: string;
  value: number;
  count: number;
};

type Props = {
  data: MonthlyTrendItem[];
};

export default function MonthlyTrendChart({ data }: Props) {
  return (
    <div className="chart-card">
      <h3>Aylık Trend</h3>
      <div className="chart-wrapper">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="label" />
            <YAxis />
            <Tooltip />
            <Bar dataKey="count" />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
