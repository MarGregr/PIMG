import { PublicClientApplication } from "@azure/msal-browser";

export const msalConfig = {
  auth: {
    clientId: "a7368f10-3995-41a3-acd1-331203adb3ff",
    authority: "https://login.microsoftonline.com/1acdc1b3-6f10-4b9b-ac4b-93b3104cda07",
    // redirectUri: "https:wkc1w796-57490.euw.devtunnels.ms/login", //adres clienta
    redirectUri: import.meta.env.VITE_LOGIN_REDIRECT_URL, //adres clienta
  },
  cache: {
    cacheLocation: "sessionStorage",
    storeAuthStateInCookie: false,
  }
};

export const loginRequest = {
  scopes: ["a7368f10-3995-41a3-acd1-331203adb3ff/.default"]
};

export const msalInstance = new PublicClientApplication(msalConfig);
await msalInstance.initialize();
