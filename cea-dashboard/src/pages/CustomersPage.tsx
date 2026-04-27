import { useEffect, useState } from "react";
import { getCustomers } from "../api/customersApi";

type CustomerItem = {
  id: number;
  name: string;
  email: string;
  companyName?: string;
  phone?: string;
  segment?: string;
  emailVerified?: boolean;
  bounceEmail?: boolean;
  createdAt?: string;
  updatedAt?: string;
};

type CustomersResponse = {
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
  items: CustomerItem[];
};

export default function CustomersPage() {
  const [data, setData] = useState<CustomersResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [searchTerm, setSearchTerm] = useState("");
  const [segment, setSegment] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 10;

  async function loadCustomers(targetPage = page) {
    try {
      setLoading(true);
      setError("");

      const result = await getCustomers({
        searchTerm,
        segment,
        page: targetPage,
        pageSize,
      });

      console.log("Customers API Result:", result);
      setData(result);
    } catch (err: any) {
      console.error("Customers error:", err);
      setError("Müşteri verileri alınamadı.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadCustomers(page);
  }, [page]);

  function handleFilter() {
    setPage(1);
    loadCustomers(1);
  }

  function handleClear() {
    setSearchTerm("");
    setSegment("");
    setPage(1);

    getCustomers({ page: 1, pageSize })
      .then((result) => {
        setData(result);
        setError("");
      })
      .catch((err) => {
        console.error("Customers error:", err);
        setError("Müşteri verileri alınamadı.");
      })
      .finally(() => setLoading(false));
  }

  function goToPreviousPage() {
    if (page > 1) {
      setPage((prev) => prev - 1);
    }
  }

  function goToNextPage() {
    if (data && page < data.totalPages) {
      setPage((prev) => prev + 1);
    }
  }

  return (
    <div>
      <h2>Customers</h2>
      <p>Müşteri listesi burada görüntülenir.</p>

      <div className="filter-card">
        <div className="filter-grid">
          <div className="filter-group">
            <label htmlFor="searchTerm">Arama</label>
            <input
              id="searchTerm"
              type="text"
              placeholder="İsim, e-posta veya firma ara"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>

          <div className="filter-group">
            <label htmlFor="segment">Segment</label>
            <select
              id="segment"
              value={segment}
              onChange={(e) => setSegment(e.target.value)}
            >
              <option value="">Tümü</option>
              <option value="VIP">VIP</option>
              <option value="Standard">Standard</option>
              <option value="Enterprise">Enterprise</option>
              <option value="B2B">B2B</option>
            </select>
          </div>

          <div className="filter-actions">
            <button className="btn-primary" onClick={handleFilter}>
              Filtrele
            </button>
            <button className="btn-secondary" onClick={handleClear}>
              Temizle
            </button>
          </div>
        </div>
      </div>

      {loading && <p>Yükleniyor...</p>}
      {error && <p>{error}</p>}

      {!loading && !error && (
        <div className="table-card">
          <div className="table-meta">
            <strong>Toplam Kayıt:</strong> {data?.totalRecords ?? 0}
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Company</th>
                <th>Phone</th>
                <th>Segment</th>
              </tr>
            </thead>
            <tbody>
              {data?.items?.length ? (
                data.items.map((customer) => (
                  <tr key={customer.id}>
                    <td>{customer.name}</td>
                    <td>{customer.email}</td>
                    <td>{customer.companyName || "-"}</td>
                    <td>{customer.phone || "-"}</td>
                    <td>{customer.segment || "-"}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={5}>Kayıt bulunamadı.</td>
                </tr>
              )}
            </tbody>
          </table>

          <div className="pagination">
            <button
              className="btn-secondary"
              onClick={goToPreviousPage}
              disabled={page === 1}
            >
              Önceki
            </button>

            <span>
              Sayfa {data?.page ?? page} / {data?.totalPages ?? 1}
            </span>

            <button
              className="btn-secondary"
              onClick={goToNextPage}
              disabled={!data || page >= data.totalPages}
            >
              Sonraki
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
