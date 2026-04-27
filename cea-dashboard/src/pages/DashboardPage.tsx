import { useEffect, useState } from "react";
import { getDashboardSummary } from "../api/analyticsApi";
import MonthlyTrendChart from "../components/charts/MonthlyTrendChart";

type MonthlyTrendItem = {
  label: string;
  value: number;
  count: number;
};

type DashboardSummary = {
  totalSurveys: number;
  activeSurveys: number;
  totalResponsesThisMonth: number;
  totalResponsesThisYear: number;
  averageNpsThisMonth: number;
  averageNpsThisYear: number;
  openComplaints: number;
  criticalComplaints: number;
  totalFeedbackCount: number;
  positiveCount: number;
  neutralCount: number;
  negativeCount: number;
  positivePercentage: number;
  neutralPercentage: number;
  negativePercentage: number;
  monthlyTrend: MonthlyTrendItem[];
};

export default function DashboardPage() {
  const [data, setData] = useState<DashboardSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    async function loadData() {
      try {
        const result = await getDashboardSummary();
        console.log("Dashboard API Result:", result);
        setData(result);
      } catch (err: any) {
        console.error("Dashboard error:", err);
        setError("Dashboard verileri alınamadı.");
      } finally {
        setLoading(false);
      }
    }

    loadData();
  }, []);

  if (loading) return <p>Yükleniyor...</p>;
  if (error) return <p>{error}</p>;

  return (
    <div>
      <h2>Dashboard</h2>
      <p>Ana özet ekranı burada olacak.</p>

      <div className="stats-grid">
        <div className="stat-card">
          <h3>Total Surveys</h3>
          <p>{data?.totalSurveys ?? 0}</p>
        </div>

        <div className="stat-card">
          <h3>Active Surveys</h3>
          <p>{data?.activeSurveys ?? 0}</p>
        </div>

        <div className="stat-card">
          <h3>Responses This Month</h3>
          <p>{data?.totalResponsesThisMonth ?? 0}</p>
        </div>

        <div className="stat-card">
          <h3>Responses This Year</h3>
          <p>{data?.totalResponsesThisYear ?? 0}</p>
        </div>

        <div className="stat-card">
          <h3>Average NPS This Month</h3>
          <p>{data?.averageNpsThisMonth ?? 0}</p>
        </div>

        <div className="stat-card">
          <h3>Average NPS This Year</h3>
          <p>{data?.averageNpsThisYear ?? 0}</p>
        </div>

        <div className="stat-card">
          <h3>Open Complaints</h3>
          <p>{data?.openComplaints ?? 0}</p>
        </div>

        <div className="stat-card">
          <h3>Critical Complaints</h3>
          <p>{data?.criticalComplaints ?? 0}</p>
        </div>
      </div>

      <div className="stats-grid" style={{ marginTop: "16px" }}>
        <div className="stat-card">
          <h3>Total Feedback</h3>
          <p>{data?.totalFeedbackCount ?? 0}</p>
        </div>

        <div className="stat-card">
          <h3>Positive</h3>
          <p>
            {data?.positiveCount ?? 0} ({data?.positivePercentage ?? 0}%)
          </p>
        </div>

        <div className="stat-card">
          <h3>Neutral</h3>
          <p>
            {data?.neutralCount ?? 0} ({data?.neutralPercentage ?? 0}%)
          </p>
        </div>

        <div className="stat-card">
          <h3>Negative</h3>
          <p>
            {data?.negativeCount ?? 0} ({data?.negativePercentage ?? 0}%)
          </p>
        </div>
      </div>
      <div style={{ marginTop: "24px" }}>
        <MonthlyTrendChart data={data?.monthlyTrend ?? []} />
      </div>
    </div>
  );
}
