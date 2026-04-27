import axiosClient from "./axiosClient";

type GetCustomersParams = {
  searchTerm?: string;
  segment?: string;
  page?: number;
  pageSize?: number;
};

export async function getCustomers(params?: GetCustomersParams) {
  const response = await axiosClient.get("/api/customers", {
    params: {
      searchTerm: params?.searchTerm || undefined,
      segment: params?.segment || undefined,
      page: params?.page ?? 1,
      pageSize: params?.pageSize ?? 20,
    },
  });

  return response.data;
}
