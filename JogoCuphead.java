 import javax.swing.*;
  import java.awt.*;
  import java.awt.event.*;
  import java.util.ArrayList;

  public class JogoCuphead extends JPanel implements ActionListener, KeyListener {
      private final int LARGURA = 800;
      private final int ALTURA = 600;

      // Jogador
      private int playerX = 100, playerY = 400, playerL = 50, playerA = 60;
      private int velX = 0, velY = 0;
      private final int GRAVIDADE = 1, FORCA_PULO = -15, VELOCIDADE_ANDAR = 5;
      private boolean noChao = false;

      // Chefe
      private int chefeX = 600, chefeY = 300, chefeL = 120, chefeA = 200;
      private int vidaChefe = 100;

      // Tiros
      private ArrayList<Rectangle> tiros = new ArrayList<>();
      private int velocidadeTiro = 10;

      // Imagens
      private Image imgPlayer, imgChefe, imgTiro;

      private Timer timer;

      public JogoCuphead() {
          this.setPreferredSize(new Dimension(LARGURA, ALTURA));
          this.setBackground(new Color(135, 206, 235));
          this.setFocusable(true);
          this.addKeyListener(this);

          // CARREGANDO IMAGENS
          // O caminho "imagens/nome.png" diz ao Java para procurar na pasta que você criou
          try {
              imgPlayer = new ImageIcon("imagens/player.png").getImage();
              imgChefe = new ImageIcon("imagens/chefe.png").getImage();
              imgTiro = new ImageIcon("imagens/tiro.png").getImage();
          } catch (Exception e) {
              System.out.println("Erro ao carregar imagens! Verifique a pasta 'imagens'.");
          }

          timer = new Timer(20, this);
          timer.start();
      }

      @Override
      protected void paintComponent(Graphics g) {
          super.paintComponent(g);

          // Chão
          g.setColor(new Color(34, 139, 34));
          g.fillRect(0, 500, LARGURA, 100);

          // Desenha Jogador (Se não tiver imagem, desenha azul)
          if (imgPlayer != null) {
              g.drawImage(imgPlayer, playerX, playerY, playerL, playerA, null);
          } else {
              g.setColor(Color.BLUE);
              g.fillRect(playerX, playerY, playerL, playerA);
          }

          // Desenha Chefe (Se não tiver imagem, desenha vermelho)
          if (imgChefe != null) {
              g.drawImage(imgChefe, chefeX, chefeY, chefeL, chefeA, null);
          } else {
              g.setColor(Color.RED);
              g.fillRect(chefeX, chefeY, chefeL, chefeA);
          }

          // Desenha Tiros
          g.setColor(Color.YELLOW);
          for (Rectangle t : tiros) {
              if (imgTiro != null) {
                  g.drawImage(imgTiro, t.x, t.y, t.width, t.height, null);
              } else {
                  g.fillRect(t.x, t.y, t.width, t.height);
              }
          }

          // Barra de Vida do Chefe
          g.setColor(Color.GRAY);
          g.fillRect(200, 20, 400, 20);
          g.setColor(Color.GREEN);
          g.fillRect(200, 20, (vidaChefe * 4), 20);
          g.setColor(Color.BLACK);
          g.drawString("VIDA DO CHEFE", 360, 15);
          g.drawString("W,A,D: Mover | L: Atirar", 10, 20);
      }

      @Override
      public void actionPerformed(ActionEvent e) {
          // Física do Jogador
          velY += GRAVIDADE;
          playerY += velY;
          playerX += velX;

          if (playerY + playerA >= 500) {
              playerY = 500 - playerA;
              velY = 0;
              noChao = true;
          } else {
              noChao = false;
          }

          // Movimentação e Colisão dos Tiros
          for (int i = 0; i < tiros.size(); i++) {
              Rectangle t = tiros.get(i);
              t.x += velocidadeTiro;

              // Tiro bateu no chefe?
              if (t.intersects(new Rectangle(chefeX, chefeY, chefeL, chefeA))) {
                  vidaChefe -= 2;
                  tiros.remove(i);
              }
              // Tiro saiu da tela?
              else if (t.x > LARGURA) {
                  tiros.remove(i);
              }
          }

          if (playerX < 0) playerX = 0;
          if (playerX + playerL > LARGURA) playerX = LARGURA - playerL;

          repaint();
      }

      @Override
      public void keyPressed(KeyEvent e) {
          int key = e.getKeyCode();
          if (key == KeyEvent.VK_A) velX = -VELOCIDADE_ANDAR;
          if (key == KeyEvent.VK_D) velX = VELOCIDADE_ANDAR;
          if (key == KeyEvent.VK_W && noChao) {
              velY = FORCA_PULO;
              noChao = false;
          }
          // ATIRAR COM 'L'
          if (key == KeyEvent.VK_L) {
              // Cria um retângulo para o tiro na posição da frente do jogador
              tiros.add(new Rectangle(playerX + playerL, playerY + (playerA/2), 15, 5));
          }
      }

      @Override
      public void keyReleased(KeyEvent e) {
          int key = e.getKeyCode();
          if (key == KeyEvent.VK_A || key == KeyEvent.VK_D) velX = 0;
      }

      @Override public void keyTyped(KeyEvent e) {}

      public static void main(String[] args) {
          JFrame frame = new JFrame("Cuphead Clone - Fase 3: Tiros e Imagens");
          JogoCuphead jogo = new JogoCuphead();
          frame.add(jogo);
          frame.pack();
          frame.setDefaultCloseOperation(JFrame.EXIT_ON_CLOSE);
          frame.setLocationRelativeTo(null);
          frame.setVisible(true);
      }
  }