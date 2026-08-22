import axios from 'axios';
import { msalInstance, loginRequest } from '../authConfig'; 

const apiClient = axios.create({
  baseURL: import.meta.env.VITE_BACKEND_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

apiClient.interceptors.request.use(
  async (config) => {
    try {
      const accounts = msalInstance.getAllAccounts();

      if (accounts.length > 0) {
        const tokenResponse = await msalInstance.acquireTokenSilent({
          ...loginRequest,
          account: accounts[0],
        });

        //Wstawienie tokea JWT do nagłówka Authorization
        config.headers.Authorization = `Bearer ${tokenResponse.accessToken}`;
      }
    } catch (error) {
      console.error('Problem z cichym pobraniem tokenu MSAL:', error);
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

//Obsługa błędów globalnych (np. gdy backend zwróci 401 Unauthorized)
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response && error.response.status === 401) {
      console.warn('Brak autoryzacji (401) z backendu. Wymagane ponowne logowanie.');
    }
    return Promise.reject(error);
  }
);

export default apiClient;
