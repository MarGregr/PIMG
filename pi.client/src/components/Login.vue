<template>
  <div class="login-container">
    <div class="login-card">
      <h2>Logowanie do systemu</h2>

      <div v-if="isLoading" class="loader-box">
        <div class="spinner"></div>
        <p>Trwa autoryzacja konta Microsoft...</p>
      </div>

      <div v-else>
        <!--<p>Aby uzyskać dostęp do aplikacji, zaloguj się za pomocą swojego konta służbowego lub szkolnego.</p>-->
        <button @click="handleLogin" class="btn-msal">
          <svg class="ms-icon" viewBox="0 0 23 23" xmlns="http://www.w3.org/2000/svg">
            <path fill="#f35325" d="M0 0h11v11H0z" />
            <path fill="#81bc06" d="M12 0h11v11H12z" />
            <path fill="#05a6f0" d="M0 12h11v11H0z" />
            <path fill="#ffba08" d="M12 12h11v11H12z" />
          </svg>
          Zaloguj się przez Microsoft
        </button>
      </div>

      <p v-if="errorMessage" class="error-msg">{{ errorMessage }}</p>
    </div>
  </div>
</template>

<script setup>
  import { ref, onMounted } from 'vue';
  import { useRouter } from 'vue-router';
  import { msalInstance, loginRequest, msalConfig } from './../authConfig';

  const router = useRouter();
  const isLoading = ref(true);
  const errorMessage = ref('');

  onMounted(async () => {
    try {
      const accounts0 = msalInstance.getAllAccounts();

      const result = await msalInstance.handleRedirectPromise();

      if (result) {
        console.log("Zalogowano pomyślnie:", result.account);
        console.log("Redirect to home");
        router.push({ name: 'home' });
        return;
      }

      const accounts = msalInstance.getAllAccounts();
      if (accounts.length > 0) {
        console.log("Redirect to home - point 2");
        router.push({ name: 'home' });
      } else {
        //Użytkownik nie jest zalogowany i nie wraca z przekierowania - wyłączany loader, pokazany przycisk
        isLoading.value = false;
      }

    } catch (error) {
      console.error("Błąd uwierzytelniania MSAL:", error);
      errorMessage.value = "Wystąpił błąd podczas logowania. Spróbuj ponownie.";
      isLoading.value = false;
    }
  });

  const handleLogin = async () => {
    try {
      isLoading.value = true;
      errorMessage.value = '';
      //Przekierowanie użytkownika na stronę Microsoftu
      await msalInstance.loginRedirect(loginRequest);
    } catch (error) {
      console.error("Błąd inicjalizacji logowania:", error);
      errorMessage.value = "Nie udało się uruchomić okna logowania.";
      isLoading.value = false;
    }
  };
</script>

<style scoped>
  .login-container {
    display: flex;
    justify-content: center;
    align-items: center;
    min-height: 80vh;
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
  }

  .login-card {
    background: #ffffff;
    padding: 40px;
    border-radius: 8px;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
    text-align: center;
    max-width: 400px;
    width: 100%;
  }

  h2 {
    color: #333;
    margin-bottom: 20px;
  }

  p {
    color: #666;
    font-size: 14px;
    line-height: 1.5;
    margin-bottom: 30px;
  }

  .btn-msal {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    background-color: #2f2f2f;
    color: white;
    border: none;
    padding: 12px 24px;
    font-size: 16px;
    font-weight: 600;
    border-radius: 4px;
    cursor: pointer;
    transition: background-color 0.2s ease;
    width: 100%;
  }

    .btn-msal:hover {
      background-color: #000000;
    }

  .ms-icon {
    width: 20px;
    height: 20px;
    margin-right: 12px;
  }

  .error-msg {
    color: #d93025;
    margin-top: 15px;
    font-weight: 500;
  }

  /* Style dla Animacji Loadera */
  .loader-box {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 20px 0;
  }

  .spinner {
    border: 4px solid rgba(0, 0, 0, 0.1);
    width: 36px;
    height: 36px;
    border-radius: 50%;
    border-left-color: #05a6f0;
    animation: spin 1s linear infinite;
    margin-bottom: 15px;
  }

  @keyframes spin {
    0% {
      transform: rotate(0deg);
    }

    100% {
      transform: rotate(360deg);
    }
  }
</style>
