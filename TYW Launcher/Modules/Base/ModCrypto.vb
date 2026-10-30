Imports System
Imports System.Security.Cryptography
Imports System.Text

''' <summary>
''' 服务端通信内容解密（与服务端 TYWServer\NetCrypto.vb 对称）。
'''
''' 协议层面没有任何变化：请求地址、方法、Notice.cfg 的分段格式、各接口的 JSON 结构全部保持原样。
''' 只是当客户端在请求头带上 X-TYW-Enc: 1 时，服务端会把响应体换成 TYW1 封包，
''' 由这里解开后继续按原来的逻辑解析。服务端没升级、或者响应本来就是明文时，
''' 客户端什么都不做（没有 X-TYW-Enc 响应头就按原文处理），所以两端可以分别升级。
'''
''' 封包格式："TYW1"(4) | IV(16) | AES-256-CBC 密文 | HMAC-SHA256(32)
''' </summary>
Public Module ModCrypto

    Private ReadOnly Magic As Byte() = Encoding.ASCII.GetBytes("TYW1")

    ''' <summary>通信主密钥（与服务端保持同一份）。</summary>
    Private ReadOnly MasterKey As Byte() = Convert.FromBase64String("E3P5nVbLKOIi/2Cb47FIglFldry6+i0jlzNmL6ZWedc=")

    Private ReadOnly KeyEnc As Byte() = Derive("TYW-ENC")
    Private ReadOnly KeyMac As Byte() = Derive("TYW-MAC")

    Private Function Derive(Label As String) As Byte()
        Using Sha As SHA256 = SHA256.Create()
            Dim LabelBytes = Encoding.UTF8.GetBytes(Label)
            Dim Buf(MasterKey.Length + LabelBytes.Length - 1) As Byte
            System.Buffer.BlockCopy(MasterKey, 0, Buf, 0, MasterKey.Length)
            System.Buffer.BlockCopy(LabelBytes, 0, Buf, MasterKey.Length, LabelBytes.Length)
            Return Sha.ComputeHash(Buf)
        End Using
    End Function

    ''' <summary>是不是一个 TYW1 封包。</summary>
    Public Function LooksEncrypted(Packet As Byte()) As Boolean
        If Packet Is Nothing OrElse Packet.Length < 4 + 16 + 16 + 32 Then Return False
        For i = 0 To Magic.Length - 1
            If Packet(i) <> Magic(i) Then Return False
        Next
        Return True
    End Function

    ''' <summary>解开 TYW1 封包；校验失败或不是封包时返回 Nothing。</summary>
    Public Function Decrypt(Packet As Byte()) As Byte()
        Try
            If Not LooksEncrypted(Packet) Then Return Nothing

            Dim BodyLen = Packet.Length - 32
            Dim Tag(31) As Byte
            System.Buffer.BlockCopy(Packet, BodyLen, Tag, 0, 32)

            Dim Expect As Byte()
            Using Hmac As New HMACSHA256(KeyMac)
                Dim Body(BodyLen - 1) As Byte
                System.Buffer.BlockCopy(Packet, 0, Body, 0, BodyLen)
                Expect = Hmac.ComputeHash(Body)
            End Using
            Dim Diff As Integer = 0
            For i = 0 To 31
                Diff = Diff Or (Tag(i) Xor Expect(i))
            Next
            If Diff <> 0 Then Return Nothing

            Dim Iv(15) As Byte
            System.Buffer.BlockCopy(Packet, Magic.Length, Iv, 0, 16)
            Dim CipherLen = BodyLen - Magic.Length - 16
            Dim Cipher(CipherLen - 1) As Byte
            System.Buffer.BlockCopy(Packet, Magic.Length + 16, Cipher, 0, CipherLen)

            Using Aes As Aes = Aes.Create()
                Aes.KeySize = 256
                Aes.Mode = CipherMode.CBC
                Aes.Padding = PaddingMode.PKCS7
                Aes.Key = KeyEnc
                Aes.IV = Iv
                Using Dec As ICryptoTransform = Aes.CreateDecryptor()
                    Return Dec.TransformFinalBlock(Cipher, 0, Cipher.Length)
                End Using
            End Using
        Catch
            Return Nothing
        End Try
    End Function

End Module
