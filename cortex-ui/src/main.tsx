import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import { Toaster } from "react-hot-toast";
import { Auth0Provider } from "@auth0/auth0-react";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <Auth0Provider
      domain="cortex-ah.us.auth0.com"
      clientId="fda3VOCFbjM3NAV6YqJvLZZkzPGn0RW3"
      authorizationParams={{
        redirect_uri: window.location.origin,
        audience: "https://cortex-api",
        scope: "openid profile email",
      }}
    >
      <App />
      <Toaster position="top-right" />
    </Auth0Provider>
  </React.StrictMode>,
);
