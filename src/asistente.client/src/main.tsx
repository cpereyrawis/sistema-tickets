import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
// Inter empaquetada: el entorno corporativo puede no llegar a la CDN de Google Fonts,
// y una fuente que no carga en silencio arruina el diseño.
import '@fontsource-variable/inter';
import './styles/tokens.css';
import './styles/app.css';

createRoot(document.getElementById('root') as HTMLElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
