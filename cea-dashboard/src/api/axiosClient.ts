import axios from "axios";

const axiosClient = axios.create({
  baseURL: "https://localhost:7145", // BURAYI SENİN BACKEND PORTUNA GÖRE DEĞİŞTİR
  withCredentials: true,
});

export default axiosClient;
