import { useEffect, useState } from "react";
import { getSurveys } from "../api/surveysApi";

type SurveyItem = {
  id: number;
  title: string;
  description?: string;
  status: number | string;
  publicToken?: string;
  analysisYear?: number;
  analysisMonth?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  requiresAuthentication?: boolean;
  allowMultipleResponses?: boolean;
  responseCount?: number;
  questionCount?: number;
  createdAt?: string;
};

export default function SurveysPage() {
  const [data, setData] = useState<SurveyItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    async function loadSurveys() {
      try {
        const result = await getSurveys();
        console.log("Surveys API Result:", result);
        setData(result);
      } catch (err: any) {
        console.error("Surveys error:", err);
        setError("Anket verileri alınamadı.");
      } finally {
        setLoading(false);
      }
    }

    loadSurveys();
  }, []);

  function formatDate(value?: string | null) {
    if (!value) return "-";
    return new Date(value).toLocaleDateString("tr-TR");
  }

  function getStatusLabel(status: number | string) {
    switch (String(status)) {
      case "1":
        return "Draft";
      case "2":
        return "Active";
      case "3":
        return "Paused";
      case "4":
        return "Completed";
      case "5":
        return "Archived";
      default:
        return String(status);
    }
  }

  function getStatusClass(status: number | string) {
    switch (String(status)) {
      case "1":
        return "status-badge status-draft";
      case "2":
        return "status-badge status-active";
      case "3":
        return "status-badge status-paused";
      case "4":
        return "status-badge status-completed";
      case "5":
        return "status-badge status-archived";
      default:
        return "status-badge";
    }
  }

  return (
    <div>
      <h2>Surveys</h2>
      <p>Anket listesi burada görüntülenir.</p>

      {loading && <p>Yükleniyor...</p>}
      {error && <p>{error}</p>}

      {!loading && !error && (
        <div className="table-card">
          <div className="table-meta">
            <strong>Toplam Anket:</strong> {data.length}
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>Title</th>
                <th>Status</th>
                <th>Responses</th>
                <th>Questions</th>
                <th>Start Date</th>
                <th>End Date</th>
              </tr>
            </thead>
            <tbody>
              {data.length ? (
                data.map((survey) => (
                  <tr key={survey.id}>
                    <td>{survey.title}</td>
                    <td>
                      <span className={getStatusClass(survey.status)}>
                        {getStatusLabel(survey.status)}
                      </span>
                    </td>
                    <td>{survey.responseCount ?? 0}</td>
                    <td>{survey.questionCount ?? 0}</td>
                    <td>{formatDate(survey.startDate)}</td>
                    <td>{formatDate(survey.endDate)}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={6}>Kayıt bulunamadı.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
