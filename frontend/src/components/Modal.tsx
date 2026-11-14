import { useEffect, useRef, type ReactNode } from 'react';

interface ModalProps {
  aberto: boolean;
  titulo: string;
  onFechar: () => void;
  children: ReactNode;
  rodape?: ReactNode;
}

/**
 * Usa o <dialog> nativo: foco preso no modal, Esc fecha e o conteúdo atrás fica inerte
 * (acessibilidade de graça, sem biblioteca).
 */
export function Modal({ aberto, titulo, onFechar, children, rodape }: ModalProps) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (aberto && !dialog.open) dialog.showModal?.();
    if (!aberto && dialog.open) dialog.close?.();
  }, [aberto]);

  return (
    <dialog
      ref={ref}
      className="modal"
      aria-labelledby="modal-titulo"
      onCancel={(e) => {
        e.preventDefault();
        onFechar();
      }}
    >
      <header className="modal__header">
        <h2 id="modal-titulo">{titulo}</h2>
        <button type="button" className="btn btn--icon" aria-label="Fechar" onClick={onFechar}>
          ×
        </button>
      </header>
      <div className="modal__body">{children}</div>
      {rodape && <footer className="modal__footer">{rodape}</footer>}
    </dialog>
  );
}
