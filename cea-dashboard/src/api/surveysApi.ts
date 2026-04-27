import axiosClient from "./axiosClient";

export async function getSurveys() {
  const response = await axiosClient.get("/api/surveys");
  return response.data;
}
