import { NavLink } from "react-router-dom";

export default function Sidebar() {
  return (
    <aside className="sidebar">
      <div className="sidebar-title">CEA Dashboard</div>

      <nav className="sidebar-nav">
        <NavLink to="/" end className="nav-item">
          Dashboard
        </NavLink>

        <NavLink to="/customers" className="nav-item">
          Customers
        </NavLink>

        <NavLink to="/surveys" className="nav-item">
          Surveys
        </NavLink>

        <NavLink to="/analytics" className="nav-item">
          Analytics
        </NavLink>
      </nav>
    </aside>
  );
}
